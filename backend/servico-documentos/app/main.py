"""API HTTP do serviço de documentos.

Contrato com o backend .NET (`LeitorDocumentosHttp`):
    POST /extrair  (multipart/form-data)
        header X-Internal-Token : str  (obrigatório se INTERNAL_TOKEN estiver
                                         configurado no .env deste serviço)
        arquivo      : UploadFile  (obrigatório)
        tipo         : str         (opcional — nome do enum TipoDocumento)
        llm_provider : str         (opcional — override de LLM_PROVIDER pra esta chamada)
        llm_model    : str         (opcional — override de LLM_MODEL)
        llm_api_key  : str         (opcional — override de LLM_API_KEY)
    -> 200 RespostaExtracao
    -> 400 override de llm_provider inválido
    -> 401 token interno ausente/inválido (só se INTERNAL_TOKEN configurado)
    -> 422 documento ilegível
    -> 413 arquivo grande demais
    -> 415 formato não suportado
    -> 500 falha interna

Os 3 campos de override existem pra que o Administrador troque provider/
modelo/chave em /configuracoes-ia (app .NET) e valha na próxima chamada,
sem reiniciar este serviço — ver `app.config.Config` e `app.pipeline.processar`.
Sem eles, cai no `.env` deste serviço como sempre.
"""
from __future__ import annotations

from fastapi import Depends, FastAPI, File, Form, Header, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from app import __version__
from app.config import Config, get_config
from app.logging_config import configurar_logging, get_logger
from app.models import RespostaExtracao, TipoDocumento
from app.pipeline import ErroExtracao, processar
from app.readers.base import DocumentoIlegivel, ErroLeitura
from app.readers.deteccao import FormatoNaoSuportado
from app.schemas import SCHEMAS

cfg = get_config()
configurar_logging(cfg.log_level)
log = get_logger("app.main")

app = FastAPI(title="ERP · Serviço de Documentos", version=__version__)


def _exigir_token_interno(x_internal_token: str | None = Header(None)) -> None:
    """Segredo compartilhado simples entre o backend .NET e este serviço —
    achado da auditoria de segurança: /extrair não exigia nada, então
    qualquer host que alcançasse a porta podia enviar documentos (e
    consumir a API key de LLM configurada). Sem INTERNAL_TOKEN configurado
    (padrão em dev), a checagem fica desativada — configure em produção;
    combina com isolamento de rede, não substitui.
    """
    if not cfg.internal_token:
        return
    if x_internal_token != cfg.internal_token:
        raise HTTPException(status_code=401, detail="Token interno ausente ou inválido.")


@app.get("/")
def raiz() -> dict:
    """Rota informativa — este serviço é a API interna de leitura de documentos,
    não a interface do ERP. A interface do ERP fica no app .NET (porta 5197)."""
    return {
        "servico": "erp-servico-documentos",
        "versao": __version__,
        "observacao": "API interna, sem interface. O ERP (login/telas) roda no app .NET.",
        "rotas": {
            "GET /health": "status e libs disponíveis",
            "GET /tipos": "tipos de documento e seus schemas de campo",
            "POST /extrair": "multipart: arquivo (obrigatório) + tipo (opcional)",
            "GET /docs": "documentação interativa (Swagger UI)",
        },
    }


@app.get("/health")
def health() -> dict:
    libs: dict[str, bool] = {}
    for nome, mod in [
        ("pypdf", "pypdf"), ("python-docx", "docx"), ("openpyxl", "openpyxl"),
        ("Pillow", "PIL"), ("pytesseract", "pytesseract"), ("pdf2image", "pdf2image"),
    ]:
        try:
            __import__(mod)
            libs[nome] = True
        except ImportError:
            libs[nome] = False
    return {
        "status": "ok",
        "versao": __version__,
        "llm_provider": cfg.llm_provider,
        "llm_ativo": cfg.usa_llm,
        "ocr_habilitado": cfg.ocr_habilitado,
        "libs": libs,
    }


@app.get("/tipos")
def tipos() -> dict:
    """Lista os tipos de documento suportados e seus schemas de campo."""
    return {
        tipo.value: {
            "descricao": s.descricao,
            "endpointErp": s.endpoint_erp,
            "campos": [
                {"nome": c.nome, "kind": c.kind, "obrigatorio": c.obrigatorio}
                for c in s.campos
            ],
        }
        for tipo, s in SCHEMAS.items()
    }


_TAMANHO_BLOCO_LEITURA = 1024 * 1024  # 1 MiB


async def _ler_com_limite(arquivo: UploadFile, limite_bytes: int, limite_mb: int) -> bytes:
    """Lê o upload em blocos, abortando assim que ultrapassa o limite — em
    vez de `await arquivo.read()` (lê tudo pra memória primeiro) e só
    depois comparar o tamanho. Sem um limite de body no nível do
    proxy/servidor ASGI, a versão antiga ainda bufferizava um arquivo bem
    maior que o limite configurado antes de rejeitá-lo (achado da
    auditoria de segurança)."""
    partes: list[bytes] = []
    total = 0
    while True:
        bloco = await arquivo.read(_TAMANHO_BLOCO_LEITURA)
        if not bloco:
            break
        total += len(bloco)
        if total > limite_bytes:
            raise HTTPException(
                status_code=413,
                detail=f"Arquivo excede o limite de {limite_mb} MB.",
            )
        partes.append(bloco)
    return b"".join(partes)


@app.post("/extrair", response_model=RespostaExtracao, response_model_by_alias=True,
          dependencies=[Depends(_exigir_token_interno)])
async def extrair(
    arquivo: UploadFile = File(...),
    tipo: str | None = Form(None),
    llm_provider: str | None = Form(None),
    llm_model: str | None = Form(None),
    llm_api_key: str | None = Form(None),
) -> RespostaExtracao:
    dados = await _ler_com_limite(arquivo, cfg.max_arquivo_bytes, cfg.max_arquivo_mb)
    if not dados:
        raise HTTPException(status_code=400, detail="Arquivo vazio.")

    tipo_informado: TipoDocumento | None = None
    if tipo:
        try:
            tipo_informado = TipoDocumento(tipo)
        except ValueError:
            validos = ", ".join(t.value for t in TipoDocumento)
            raise HTTPException(
                status_code=400,
                detail=f"tipo '{tipo}' inválido. Válidos: {validos}",
            )

    cfg_override: Config | None = None
    if llm_provider and llm_api_key:
        try:
            cfg_override = Config(
                llm_provider=llm_provider,
                llm_api_key=llm_api_key,
                llm_model=llm_model or cfg.llm_model,
                llm_timeout_segundos=cfg.llm_timeout_segundos,
            )
        except ValidationError:
            validos = ", ".join(("none", "anthropic", "openai", "gemini"))
            raise HTTPException(
                status_code=400,
                detail=f"llm_provider '{llm_provider}' inválido. Válidos: {validos}",
            )

    nome = arquivo.filename or "sem-nome"
    try:
        return processar(dados, nome, tipo_informado, cfg_override=cfg_override)
    except DocumentoIlegivel as e:
        log.warning("422 documento ilegível (%s): %s", nome, e)
        raise HTTPException(status_code=422, detail=str(e))
    except FormatoNaoSuportado as e:
        raise HTTPException(status_code=415, detail=str(e))
    except (ErroLeitura, ErroExtracao) as e:
        # str(e) pode conter detalhe interno (ex.: "poppler instalado?",
        # caminho de arquivo temporário) — fica só no log, igual ao branch
        # de baixo; o cliente só recebe o tipo do erro (achado da auditoria
        # de segurança).
        log.exception("500 falha ao processar %s", nome)
        raise HTTPException(status_code=500, detail=f"Falha ao processar o documento: {type(e).__name__}")
    except Exception as e:  # rede de segurança — nunca vazar stack pro cliente
        log.exception("500 inesperado ao processar %s", nome)
        raise HTTPException(status_code=500, detail=f"Erro interno: {type(e).__name__}")


@app.exception_handler(HTTPException)
async def _http_exc(_, exc: HTTPException):
    return JSONResponse(status_code=exc.status_code, content={"erro": exc.detail})
