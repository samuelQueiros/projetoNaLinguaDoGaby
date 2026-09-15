"""Orquestra as 3 sub-etapas da EXTRAÇÃO (a etapa 1 do fluxo do módulo):

    detectar arquivo  ->  ler (com OCR)  ->  classificar tipo de documento
    ->  aplicar schema  ->  extrair campos (regex + LLM)  ->  montar resposta

CONFIRMAÇÃO (human-in-the-loop) e INTEGRAÇÃO COM O ERP acontecem no backend
.NET — ver `docs/modulo-ia-documentos.md`. Este serviço é stateless.
"""
from __future__ import annotations

import time
import uuid

from app.classificacao import classificar
from app.config import Config, get_config
from app.extracao import extrair_por_llm, extrair_por_regex
from app.extracao.llm_extractor import ErroLLM
from app.logging_config import get_logger
from app.models import (
    CampoExtraido,
    DocumentoBruto,
    InfoArquivo,
    RespostaExtracao,
    StatusCampo,
    TipoDocumento,
)
from app.readers import detectar_formato, obter_leitor
from app.schemas import CampoSpec, obter_schema
from app.validacao import validar_cnpj_ou_cpf, validar_linha_digitavel

log = get_logger(__name__)


class ErroExtracao(Exception):
    """Falha genérica de pipeline -> HTTP 500."""


def _melhor(campo_regex: CampoExtraido, campo_llm: CampoExtraido | None) -> CampoExtraido:
    """Funde os dois extractors: fica com o de maior confiança; se o LLM achou
    e o regex não, usa o LLM (e vice-versa). Marca 'ambiguo' quando discordam."""
    if campo_llm is None or campo_llm.status == StatusCampo.NAO_ENCONTRADO:
        return campo_regex
    if campo_regex.status == StatusCampo.NAO_ENCONTRADO:
        return campo_llm

    concordam = (campo_regex.valor or "").strip() == (campo_llm.valor or "").strip()
    vencedor = campo_llm if campo_llm.confianca >= campo_regex.confianca else campo_regex
    perdedor = campo_regex if vencedor is campo_llm else campo_llm

    if concordam:
        # dois métodos independentes bateram -> sobe a confiança
        return vencedor.model_copy(update={
            "confianca": round(min(0.99, max(vencedor.confianca, perdedor.confianca) + 0.1), 2),
            "status": StatusCampo.EXTRAIDO,
        })
    return vencedor.model_copy(update={
        "status": StatusCampo.AMBIGUO,
        "candidatos": [c for c in {perdedor.valor} if c],
    })


def _validar_deterministicamente(campo: CampoExtraido, spec: CampoSpec) -> tuple[CampoExtraido, str | None]:
    """Confere CNPJ/CPF/linha digitável por dígito verificador — 100%
    determinístico, sem IA. Concorda com a extração -> sobe a confiança
    (confirmação matemática independente); discorda -> derruba pra
    'baixa_confianca' e gera um aviso, mesmo que o LLM estivesse "confiante".
    Ver `app/validacao.py` para o porquê disso importar tanto pra
    resultado bom com modelo fraco."""
    if campo.valor is None:
        return campo, None

    if spec.kind in ("cnpj", "cpf"):
        valido = validar_cnpj_ou_cpf(campo.valor)
    elif spec.kind == "linha_digitavel":
        valido = validar_linha_digitavel(campo.valor)
    else:
        return campo, None

    if valido is None:  # formato fora do que sabemos validar (ex.: boleto de arrecadação)
        return campo, None

    if valido:
        atualizado = campo.model_copy(update={
            "confianca": round(min(0.99, campo.confianca + 0.15), 2),
            "status": StatusCampo.EXTRAIDO,
        })
        return atualizado, None

    atualizado = campo.model_copy(update={
        "confianca": round(campo.confianca * 0.3, 2),
        "status": StatusCampo.BAIXA_CONFIANCA,
    })
    aviso = f"'{spec.nome}' extraído ('{campo.valor}') não passa na validação de dígito verificador — confira manualmente."
    return atualizado, aviso


def _resolver_tipo(
    texto: str, tipo_informado: TipoDocumento | None
) -> tuple[TipoDocumento, str, float]:
    if tipo_informado is not None:
        return tipo_informado, "informado", 1.0
    tipo, conf = classificar(texto)
    return tipo, "classificacao", conf


def processar(
    dados: bytes,
    nome_arquivo: str,
    tipo_informado: TipoDocumento | None = None,
    cfg_override: Config | None = None,
) -> RespostaExtracao:
    lote_id = str(uuid.uuid4())
    t0 = time.perf_counter()
    log.info("[%s] início — arquivo=%s (%d bytes)", lote_id, nome_arquivo, len(dados))
    avisos: list[str] = []

    # 1. formato do arquivo
    # FormatoNaoSuportado propaga direto (não vira ErroExtracao) — main.py
    # tem um handler dedicado pra ela que devolve 415, documentado no
    # próprio contrato do endpoint. Antes ela era reembalada aqui, e esse
    # handler nunca era alcançado: todo upload de formato não suportado
    # virava 500 genérico em vez do 415 documentado (achado da auditoria
    # de segurança).
    formato = detectar_formato(dados, nome_arquivo)

    # 2. leitura (pode fazer OCR)
    leitor = obter_leitor(formato)
    doc: DocumentoBruto = leitor.ler(dados, nome_arquivo)   # DocumentoIlegivel sobe -> 422
    # cfg_override vem do .NET quando o Administrador configurou provider/
    # modelo/chave em /configuracoes-ia — sem ele, cai no .env global de sempre.
    cfg = cfg_override or get_config()
    if cfg.log_texto_bruto:
        log.debug("[%s] texto extraído:\n%s", lote_id, doc.texto[:1500])
    log.info("[%s] lido: %d chars, %d pág, ocr=%s", lote_id, len(doc.texto), doc.paginas, doc.ocr_usado)

    # 3. tipo de documento
    tipo, tipo_origem, conf_tipo = _resolver_tipo(doc.texto, tipo_informado)
    log.info("[%s] tipo=%s (origem=%s, conf=%.2f)", lote_id, tipo.value, tipo_origem, conf_tipo)
    if tipo == TipoDocumento.NAO_IDENTIFICADO and tipo_informado is None:
        avisos.append("Tipo de documento não identificado — extração genérica. "
                      "Informe o tipo no upload para melhor resultado.")

    # 4. schema + 5. extração
    schema = obter_schema(tipo)
    campos_regex = extrair_por_regex(doc.texto, schema)

    campos_llm: dict[str, CampoExtraido] = {}
    if cfg.usa_llm:
        try:
            # campos_regex vira grounding no prompt — o LLM confirma/corrige
            # um candidato em vez de extrair do zero (ver nota de design em
            # app/extracao/llm_extractor.py).
            campos_llm = extrair_por_llm(doc.texto, schema, cfg, campos_regex)
        except ErroLLM as e:
            log.warning("[%s] LLM falhou, seguindo só com regex: %s", lote_id, e)
            avisos.append("Extração assistida por IA indisponível — usados só padrões automáticos.")

    # 6. merge + validação determinística (CNPJ/CPF/linha digitável) +
    #    campos obrigatórios pendentes
    campos_final: list[CampoExtraido] = []
    pendentes: list[str] = []
    for spec in schema.campos:
        campo = _melhor(campos_regex[spec.nome], campos_llm.get(spec.nome))
        campo, aviso_validacao = _validar_deterministicamente(campo, spec)
        if aviso_validacao:
            avisos.append(aviso_validacao)
        campos_final.append(campo)
        falta = campo.status in (StatusCampo.NAO_ENCONTRADO, StatusCampo.AMBIGUO) or campo.valor is None
        baixa = campo.status == StatusCampo.BAIXA_CONFIANCA
        if spec.obrigatorio and (falta or baixa):
            pendentes.append(spec.nome)

    if pendentes:
        avisos.append("Revisão manual necessária: " + ", ".join(pendentes))

    # confiança geral: média das confianças dos campos obrigatórios (ou de todos)
    base = [c for c in campos_final if schema.campos_por_nome[c.nome].obrigatorio] or campos_final
    conf_geral = round(sum(c.confianca for c in base) / len(base), 2) if base else 0.0

    resp = RespostaExtracao(
        loteId=lote_id,
        tipoDetectado=tipo,
        tipoOrigem=tipo_origem,
        confiancaGeral=conf_geral,
        arquivo=InfoArquivo(
            nome=nome_arquivo, formato=formato,
            ocr_usado=doc.ocr_usado, paginas=doc.paginas,
        ),
        campos=campos_final,
        camposObrigatoriosPendentes=pendentes,
        avisos=avisos,
    )
    log.info("[%s] fim em %dms — conf_geral=%.2f, pendentes=%s",
             lote_id, int((time.perf_counter() - t0) * 1000), conf_geral, pendentes)
    return resp
