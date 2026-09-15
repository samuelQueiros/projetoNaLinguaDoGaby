"""Extração de campos por regex/heurística — rápida, offline, sem custo.

Roda sempre. Quando o LLM está ligado, o pipeline usa o resultado do LLM como
preferência e cai para o regex quando o LLM não achou o campo.

Cada `CampoSpec.kind` tem um par (localizador, normalizador).
"""
from __future__ import annotations

import re
import unicodedata


def _sem_acento(s: str) -> str:
    return "".join(
        c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn"
    ).lower()

from app.models import CampoExtraido, StatusCampo
from app.schemas import CampoSpec, SchemaDocumento

# --------------------------------------------------------------------------- #
# Normalizadores: string bruta -> string canônica                            #
# --------------------------------------------------------------------------- #

_MESES = {
    "jan": "01", "fev": "02", "mar": "03", "abr": "04", "mai": "05", "jun": "06",
    "jul": "07", "ago": "08", "set": "09", "out": "10", "nov": "11", "dez": "12",
}


def _norm_valor(bruto: str) -> str | None:
    m = re.search(r"(\d{1,3}(?:[.\s]\d{3})*(?:,\d{2})|\d+,\d{2}|\d+\.\d{2}|\d+)", bruto)
    if not m:
        return None
    n = m.group(1).replace(" ", "")
    if "," in n:                       # formato BR: 1.234,56
        n = n.replace(".", "").replace(",", ".")
    try:
        return f"{float(n):.2f}"
    except ValueError:
        return None


def _norm_data(bruto: str) -> str | None:
    bruto = bruto.strip().lower()
    m = re.search(r"(\d{4})-(\d{2})-(\d{2})", bruto)
    if m:
        return m.group(0)
    m = re.search(r"(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{2,4})", bruto)
    if m:
        d, mo, y = m.groups()
        y = y if len(y) == 4 else ("20" + y)
        return f"{int(y):04d}-{int(mo):02d}-{int(d):02d}"
    m = re.search(r"(\d{1,2})\s+de\s+([a-zç]+)\s+de\s+(\d{4})", bruto)
    if m:
        d, mes_txt, y = m.groups()
        mes = _MESES.get(mes_txt[:3])
        if mes:
            return f"{int(y):04d}-{mes}-{int(d):02d}"
    return None


def _norm_digitos(bruto: str, n: int) -> str | None:
    dig = re.sub(r"\D", "", bruto)
    return dig if len(dig) == n else (dig or None)


def _norm_cnpj(b: str) -> str | None:
    return _norm_digitos(b, 14)


def _norm_cpf(b: str) -> str | None:
    return _norm_digitos(b, 11)


def _norm_linha_digitavel(b: str) -> str | None:
    dig = re.sub(r"\D", "", b)
    return dig if len(dig) in (47, 48) else (dig or None)


def _norm_numero(b: str) -> str | None:
    m = re.search(r"[A-Za-z]?\d[\d./\-]{1,}", b)
    return m.group(0).strip(".-/") if m else None


def _norm_texto(b: str) -> str | None:
    t = " ".join(b.split())
    # descarta um rótulo residual colado no início ("Razão Social: ACME" -> "ACME"),
    # mas só quando o que vem antes do ':' parece mesmo um rótulo (só letras/espaço)
    if re.match(r"[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ ]{0,24}:\s*\S", t):
        t = t.split(":", 1)[1]
    # corta um CNPJ/CPF que veio colado na mesma linha do nome
    t = re.split(r"\s*[-–,]?\s*(?:cnpj|cpf|c\.n\.p\.j)\b", t, maxsplit=1, flags=re.IGNORECASE)[0]
    t = t.strip(" :;-\t|")
    return t[:200] or None


_NORMALIZADORES = {
    "valor": _norm_valor,
    "data": _norm_data,
    "cnpj": _norm_cnpj,
    "cpf": _norm_cpf,
    "linha_digitavel": _norm_linha_digitavel,
    "numero": _norm_numero,
    "texto": _norm_texto,
}

# padrões "livres" (sem rótulo) por kind — usados como fallback/validação
_PADROES_LIVRES = {
    "cnpj": re.compile(r"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}"),
    "linha_digitavel": re.compile(r"\d{5}[.\s]?\d{5}[\s.]?\d{5}[.\s]?\d{6}[\s.]?\d{5}[.\s]?\d{6}[\s.]?\d\s?\d{14}"),
}


def _buscar_com_rotulo(texto: str, rotulos: tuple[str, ...]) -> tuple[str, str] | None:
    """Procura 'rotulo ... valor' na mesma linha ou na linha seguinte.
    Retorna (trecho_bruto_apos_rotulo, linha_de_origem)."""
    linhas = texto.split("\n")
    for i, linha in enumerate(linhas):
        low = _sem_acento(linha)
        for rot in rotulos:
            pos = low.find(_sem_acento(rot))
            if pos == -1:
                continue
            resto = linha[pos + len(rot):].strip(" :=\t|-")
            if len(resto) >= 2:
                return resto, linha.strip()
            # valor pode estar na próxima linha (layouts em coluna)
            if i + 1 < len(linhas) and linhas[i + 1].strip():
                return linhas[i + 1].strip(), f"{linha.strip()} / {linhas[i + 1].strip()}"
    return None


def extrair_campo(texto: str, spec: CampoSpec) -> CampoExtraido:
    normalizar = _NORMALIZADORES.get(spec.kind, _norm_texto)

    bruto = None
    origem = None
    confianca = 0.0

    achado = _buscar_com_rotulo(texto, spec.rotulos)
    if achado:
        bruto, origem = achado
        confianca = 0.75

    if bruto is None and spec.kind in _PADROES_LIVRES:
        m = _PADROES_LIVRES[spec.kind].search(texto)
        if m:
            bruto = m.group(0)
            origem = _linha_de(texto, m.start())
            confianca = 0.6  # achou o padrão, mas sem rótulo confirmando

    if bruto is None:
        return CampoExtraido(nome=spec.nome, status=StatusCampo.NAO_ENCONTRADO)

    valor = normalizar(bruto)
    if valor is None:
        return CampoExtraido(
            nome=spec.nome, valor=None, confianca=0.2,
            status=StatusCampo.AMBIGUO, origem=(origem or bruto)[:180],
        )

    # penaliza tipos com validação forte que não bateram o tamanho esperado
    if spec.kind == "cnpj" and len(valor) != 14:
        confianca *= 0.5
    if spec.kind == "linha_digitavel" and len(valor) not in (47, 48):
        confianca *= 0.5

    status = StatusCampo.EXTRAIDO if confianca >= spec.confianca_minima else StatusCampo.BAIXA_CONFIANCA
    return CampoExtraido(
        nome=spec.nome, valor=valor, confianca=round(confianca, 2),
        status=status, origem=(origem or bruto)[:180],
    )


def _linha_de(texto: str, offset: int) -> str:
    ini = texto.rfind("\n", 0, offset) + 1
    fim = texto.find("\n", offset)
    return texto[ini: fim if fim != -1 else None].strip()


def extrair_por_regex(texto: str, schema: SchemaDocumento) -> dict[str, CampoExtraido]:
    return {spec.nome: extrair_campo(texto, spec) for spec in schema.campos}
