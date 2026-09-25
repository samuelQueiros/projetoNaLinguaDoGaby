"""Classificação do TIPO DE DOCUMENTO por palavras-chave.

Simples de propósito: pontua cada tipo pela contagem de keywords encontradas
no texto (peso maior para expressões mais específicas). O LLM, quando ligado,
pode sobrescrever isso no pipeline.

Para afinar: edite `PALAVRAS_CHAVE`. Cada item é (regex, peso).
"""
from __future__ import annotations

import re

from app.models import TipoDocumento

# (padrão, peso) — padrões são casados case-insensitive sobre o texto todo
PALAVRAS_CHAVE: dict[TipoDocumento, list[tuple[str, float]]] = {
    TipoDocumento.NOTA_FISCAL: [
        (r"\bnota fiscal\b", 2.0),
        (r"\bnf-?e\b", 2.0),
        (r"\bnfs-?e\b", 2.5),
        (r"danfe", 3.0),
        (r"natureza da opera[çc][ãa]o", 2.0),
        (r"\bicms\b", 1.0),
        (r"chave de acesso", 2.0),
    ],
    TipoDocumento.BOLETO: [
        (r"linha digit[áa]vel", 3.0),
        (r"ficha de compensa[çc][ãa]o", 3.0),
        (r"c[óo]digo de barras", 1.5),
        (r"\bcedente\b", 2.0),
        (r"nosso n[úu]mero", 2.5),
        (r"local de pagamento", 1.5),
        (r"\bbenefici[áa]rio\b", 1.0),
    ],
    TipoDocumento.PEDIDO_COMPRA: [
        (r"pedido de compra", 3.0),
        (r"ordem de compra", 3.0),
        (r"\bpurchase order\b", 2.0),
        (r"\bo\.?c\.?\s*n[ºo]", 2.0),
        (r"condi[çc][ãa]o de pagamento", 0.8),
        (r"previs[ãa]o de entrega", 1.5),
    ],
    TipoDocumento.CONTRATO: [
        (r"\bcontrato\b", 1.5),
        (r"cl[áa]usula", 2.0),
        (r"contratante.{0,40}contratad", 3.0),
        (r"v[ií]gencia", 1.5),
        (r"foro da comarca", 2.5),
        (r"partes acordam", 2.0),
        (r"objeto do contrato", 2.0),
    ],
    TipoDocumento.COMPROVANTE_PAGAMENTO: [
        (r"comprovante de pagamento", 3.0),
        (r"comprovante de transfer[êe]ncia", 3.0),
        (r"comprovante de pix", 3.0),
        (r"\bpix\b", 1.0),
        (r"\bted\b|\bdoc\b", 1.0),
        (r"autentica[çc][ãa]o", 1.0),
        (r"favorecido", 1.5),
        (r"id da transa[çc][ãa]o|c[óo]digo da transa[çc][ãa]o", 2.0),
    ],
}

_COMPILADO = {
    tipo: [(re.compile(p, re.IGNORECASE), peso) for p, peso in regras]
    for tipo, regras in PALAVRAS_CHAVE.items()
}

# limiar mínimo de pontuação para não cair em NAO_IDENTIFICADO
_LIMIAR = 2.0


def classificar(texto: str) -> tuple[TipoDocumento, float]:
    """Retorna (tipo, confianca 0-1)."""
    if not texto.strip():
        return TipoDocumento.NAO_IDENTIFICADO, 0.0

    pontos: dict[TipoDocumento, float] = {}
    for tipo, regras in _COMPILADO.items():
        s = sum(peso for rx, peso in regras if rx.search(texto))
        if s:
            pontos[tipo] = s

    if not pontos:
        return TipoDocumento.NAO_IDENTIFICADO, 0.0

    tipo_top = max(pontos, key=pontos.get)
    top = pontos[tipo_top]
    if top < _LIMIAR:
        return TipoDocumento.NAO_IDENTIFICADO, round(top / (_LIMIAR * 2), 2)

    # confiança = separação entre o 1º e o 2º colocado, saturada
    segundo = sorted(pontos.values(), reverse=True)
    margem = top - (segundo[1] if len(segundo) > 1 else 0.0)
    conf = min(0.55 + margem / 8.0, 0.97)
    return tipo_top, round(conf, 2)
