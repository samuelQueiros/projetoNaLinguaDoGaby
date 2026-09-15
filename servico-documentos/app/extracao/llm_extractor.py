"""Extração de campos por LLM — OPCIONAL.

O provider é escolhido por env (`LLM_PROVIDER`) ou por override vindo do
.NET (ver `app/main.py`): `anthropic`, `openai`, `gemini` ou `none`. O
backend .NET nunca sabe COMO cada provider é chamado — só conhece o
contrato HTTP.

Se a lib do provider não estiver instalada, a chamada falha graciosamente e o
pipeline segue só com o regex_extractor.

DESIGN PENSADO PRA FUNCIONAR BEM MESMO COM MODELO FRACO/BARATO — 4 técnicas,
em ordem de impacto:

1. **Saída estruturada garantida** (Gemini: `response_json_schema`; OpenAI:
   `response_format` json_object). Um modelo fraco erra formatação de JSON
   com muito mais frequência que um modelo caro — tirar essa tarefa da mão
   do modelo (o provider força o formato) elimina uma classe inteira de
   falha antes mesmo de pensar em "o modelo entendeu o documento".
2. **Grounding pelos candidatos do regex** (`campos_regex` em
   `_montar_prompt`): em vez de pedir pro modelo "extrair do zero" (tarefa
   geradora, difícil), a gente já manda um candidato encontrado por busca de
   padrão e pede pra CONFIRMAR ou CORRIGIR (tarefa de verificação, muito
   mais fácil — inclusive pra um modelo fraco). Ver `app/pipeline.py` e
   `app/extracao/regex_extractor.py`.
3. **Validação determinística pós-LLM** (`app/validacao.py`): dígito
   verificador de CNPJ/CPF/linha digitável não depende do modelo ter
   "entendido" nada — é matemática. Aplicado depois, no pipeline.
4. **Prompt com calibração explícita de confiança + consciência de OCR**
   (`_montar_prompt` abaixo) — instruções concretas, não "seja preciso".

>>> AJUSTE AQUI: o prompt de sistema e o mapeamento de modelo por provider. <<<
"""
from __future__ import annotations

import json
import random
import re
import time

from app.config import Config, get_config
from app.logging_config import get_logger
from app.models import CampoExtraido, StatusCampo
from app.schemas import SchemaDocumento

log = get_logger(__name__)


class ErroLLM(Exception):
    pass


# --------------------------------------------------------------------------- #
# Prompt                                                                       #
# --------------------------------------------------------------------------- #

_SISTEMA = """\
Você é um agente financeiro especialista em documentos brasileiros — notas \
fiscais, boletos, contratos, comprovantes de pagamento e pedidos de compra. \
Sua extração alimenta a fila de aprovação de um Administrador financeiro: \
ele vai confiar no que você marcar como alta confiança, então precisão \
importa mais que "preencher tudo".

REGRAS OBRIGATÓRIAS:
1. Responda SOMENTE com um objeto JSON válido — sem texto ao redor, sem \
comentário, sem cercas de código (```).
2. Para cada campo pedido, devolva um objeto: \
{"valor": <string ou null>, "confianca": <0 a 1>, "origem": <trecho exato \
do texto de onde tirou o valor>}.
3. NUNCA invente um valor. Se o campo não aparecer no texto nem nos \
candidatos sugeridos, use valor null e confianca 0 — é preferível deixar \
em branco pra revisão humana do que arriscar um valor errado.
4. Alguns campos já vêm com um candidato encontrado por outro método (busca \
por padrão de texto). Trate como um rascunho a conferir, não como verdade: \
confirme se bate exatamente com o texto, corrija se estiver errado (pegou \
o rótulo errado, cortou um dígito, misturou com outro número perto), ou \
descarte se não tiver base nenhuma no texto.

CALIBRAÇÃO DE CONFIANÇA (use com rigor — isso decide se um humano revisa \
manualmente ou não):
- 0.90 a 1.00: valor explícito, sem ambiguidade, perto de um rótulo claro \
(ex.: "VALOR TOTAL DA NOTA: R$ 1.234,56").
- 0.70 a 0.89: valor presente mas exigiu alguma inferência (rótulo \
genérico, formatação incomum, valor um pouco distante do rótulo).
- 0.40 a 0.69: suposição razoável, mas o texto é ambíguo, truncado, ou há \
mais de um candidato plausível no documento.
- 0.00 a 0.39: você não tem certeza real, está chutando, ou o candidato \
sugerido parece estar errado.

TEXTO COM ERROS DE OCR: o texto abaixo pode vir de leitura óptica de \
documento escaneado ou foto — espere trocas comuns (0/O, 1/l/I, 5/S, 8/B, \
espaços quebrando números ou palavras no meio). No campo "origem", copie o \
trecho EXATAMENTE como está no texto (erros incluídos, não corrija a \
grafia) — mas normalize o "valor" corretamente, interpretando o que o \
texto provavelmente quer dizer.

FORMATOS DE SAÍDA (campo "valor"):
- Monetário: "1234.56" (ponto decimal, sem separador de milhar, sem "R$").
- Data: "AAAA-MM-DD". Se faltar o ano, tente inferir pelo contexto do \
documento; se não der pra ter certeza, use null.
- CNPJ/CPF: só dígitos, sem pontuação.
- Linha digitável de boleto: só dígitos, sem pontos nem espaços.
"""


def _montar_prompt(
    texto: str,
    schema: SchemaDocumento,
    campos_regex: dict[str, CampoExtraido] | None = None,
) -> tuple[str, str]:
    campos_desc = "\n".join(
        f'- "{c.nome}" ({c.kind}){" [OBRIGATÓRIO]" if c.obrigatorio else ""}: '
        f'{c.dica_llm or "rótulos típicos: " + ", ".join(c.rotulos)}'
        for c in schema.campos
    )

    candidatos_linhas = []
    for nome, campo in (campos_regex or {}).items():
        if campo.valor:
            candidatos_linhas.append(
                f'- "{nome}": candidato "{campo.valor}" (confiança da busca por padrão: '
                f'{campo.confianca:.0%}), encontrado em: "{campo.origem}"'
            )
    candidatos_txt = "\n".join(candidatos_linhas) if candidatos_linhas else "(nenhum candidato pré-encontrado)"

    usuario = (
        f"Tipo do documento: {schema.tipo.value}\n\n"
        f"Campos a extrair:\n{campos_desc}\n\n"
        f"Candidatos já encontrados por outro método (confirme, corrija ou descarte):\n{candidatos_txt}\n\n"
        f"Texto do documento (pode conter erros de OCR):\n\"\"\"\n{texto[:12000]}\n\"\"\"\n\n"
        f'Retorne: {{ "<nome_do_campo>": {{ "valor", "confianca", "origem" }}, ... }} para todos os campos pedidos.'
    )
    return _SISTEMA, usuario


def _schema_json_llm(schema: SchemaDocumento) -> dict:
    """JSON Schema do objeto de resposta esperado — usado pelo Gemini
    (`response_json_schema`) pra GARANTIR a forma da saída, em vez de só
    pedir educadamente no prompt. Ver nota de design no topo do arquivo."""
    campo_schema = {
        "type": "object",
        "properties": {
            "valor": {"anyOf": [{"type": "string"}, {"type": "null"}]},
            "confianca": {"type": "number"},
            "origem": {"anyOf": [{"type": "string"}, {"type": "null"}]},
        },
        "required": ["valor", "confianca"],
    }
    return {
        "type": "object",
        "properties": {c.nome: campo_schema for c in schema.campos},
        "required": [c.nome for c in schema.campos],
    }


# --------------------------------------------------------------------------- #
# Chamada a cada provider                                                     #
# --------------------------------------------------------------------------- #

def _chamar_anthropic(sistema: str, usuario: str, cfg, _schema: SchemaDocumento) -> str:
    # _schema não é usado aqui (só o Gemini tem response_json_schema) —
    # mantido na assinatura só pra bater com _PROVIDERS, que despacha as
    # 3 funções de forma uniforme.
    import anthropic  # type: ignore

    client = anthropic.Anthropic(api_key=cfg.llm_api_key, timeout=cfg.llm_timeout_segundos)
    resp = client.messages.create(
        model=cfg.llm_model,
        max_tokens=2000,
        system=sistema,
        messages=[{"role": "user", "content": usuario}],
    )
    return "".join(b.text for b in resp.content if getattr(b, "type", None) == "text")


def _chamar_openai(sistema: str, usuario: str, cfg, _schema: SchemaDocumento) -> str:
    from openai import OpenAI  # type: ignore

    client = OpenAI(api_key=cfg.llm_api_key, timeout=cfg.llm_timeout_segundos)
    resp = client.chat.completions.create(
        model=cfg.llm_model,
        response_format={"type": "json_object"},
        messages=[
            {"role": "system", "content": sistema},
            {"role": "user", "content": usuario},
        ],
    )
    return resp.choices[0].message.content or "{}"


def _chamar_gemini(sistema: str, usuario: str, cfg, schema: SchemaDocumento) -> str:
    from google import genai  # type: ignore
    from google.genai import types  # type: ignore

    client = genai.Client(
        api_key=cfg.llm_api_key,
        http_options=types.HttpOptions(timeout=cfg.llm_timeout_segundos * 1000),
    )
    resp = client.models.generate_content(
        model=cfg.llm_model,
        contents=usuario,
        config=types.GenerateContentConfig(
            system_instruction=sistema,
            response_mime_type="application/json",
            response_json_schema=_schema_json_llm(schema),
            temperature=0,
        ),
    )
    return resp.text or "{}"


_PROVIDERS = {"anthropic": _chamar_anthropic, "openai": _chamar_openai, "gemini": _chamar_gemini}


def _chamar_com_retry(fn, sistema: str, usuario: str, cfg: Config, schema: SchemaDocumento) -> str:
    """Repete a chamada ao provider em caso de falha (rate limit/erro 5xx
    passageiro são o caso mais comum na prática) antes de desistir — mesmo
    padrão de retry+backoff+jitter já usado em contrib/erp_client.py
    (achado da auditoria de qualidade: aqui a primeira falha já derrubava
    pro fallback regex-only, sem tentar de novo). Lib não instalada nunca é
    repetível — falha na primeira tentativa.
    """
    ultima_excecao: Exception | None = None
    for tentativa in range(1, cfg.llm_max_tentativas + 1):
        try:
            return fn(sistema, usuario, cfg, schema)
        except ImportError as e:
            raise ErroLLM(f"Lib do provider '{cfg.llm_provider}' não instalada.") from e
        except Exception as e:  # timeout, rate limit, auth...
            ultima_excecao = e
            log.warning("Tentativa %d/%d de chamar o LLM (%s) falhou: %s",
                        tentativa, cfg.llm_max_tentativas, cfg.llm_provider, e)
            if tentativa < cfg.llm_max_tentativas:
                espera = 2 ** (tentativa - 1)
                espera += random.uniform(0, espera * 0.25)  # jitter
                time.sleep(espera)

    raise ErroLLM(f"Falha na chamada ao LLM após {cfg.llm_max_tentativas} tentativa(s): {ultima_excecao}") from ultima_excecao


def _parse_json(bruto: str) -> dict:
    bruto = bruto.strip()
    m = re.search(r"\{.*\}", bruto, re.DOTALL)  # tolera cercas de código / texto extra
    if not m:
        raise ErroLLM(f"Resposta do LLM não continha JSON: {bruto[:200]}")
    return json.loads(m.group(0))


def extrair_por_llm(
    texto: str,
    schema: SchemaDocumento,
    cfg: Config | None = None,
    campos_regex: dict[str, CampoExtraido] | None = None,
) -> dict[str, CampoExtraido]:
    """Pode levantar `ErroLLM`. O pipeline captura e segue só com o regex.

    `cfg` é opcional — quando vem do `.NET` como override por requisição
    (ver `/extrair` em `app/main.py`), usa provider/modelo/chave definidos
    pelo Administrador em `/configuracoes-ia` em vez do `.env` global. Sem
    override, cai no comportamento de sempre (`get_config()`, cacheado).

    `campos_regex` é o resultado do `regex_extractor` pra este mesmo
    documento — vira "candidato a confirmar" no prompt (grounding, ver nota
    de design no topo do arquivo). Sem isso, o LLM extrai do zero.
    """
    cfg = cfg or get_config()
    if not cfg.usa_llm:
        return {}

    fn = _PROVIDERS.get(cfg.llm_provider)
    if fn is None:
        raise ErroLLM(f"Provider LLM desconhecido: {cfg.llm_provider}")

    sistema, usuario = _montar_prompt(texto, schema, campos_regex)
    bruto = _chamar_com_retry(fn, sistema, usuario, cfg, schema)

    dados = _parse_json(bruto)

    resultado: dict[str, CampoExtraido] = {}
    for spec in schema.campos:
        item = dados.get(spec.nome)
        if not isinstance(item, dict) or item.get("valor") in (None, "", "null"):
            resultado[spec.nome] = CampoExtraido(nome=spec.nome, status=StatusCampo.NAO_ENCONTRADO)
            continue
        conf = float(item.get("confianca") or 0.0)
        conf = max(0.0, min(conf, 1.0))
        status = StatusCampo.EXTRAIDO if conf >= spec.confianca_minima else StatusCampo.BAIXA_CONFIANCA
        resultado[spec.nome] = CampoExtraido(
            nome=spec.nome,
            valor=str(item["valor"]),
            confianca=round(conf, 2),
            status=status,
            origem=(str(item.get("origem") or "")[:180]) or None,
        )
    return resultado
