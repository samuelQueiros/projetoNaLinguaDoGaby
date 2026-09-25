"""Registry de leitores por formato de arquivo.

Para adicionar um formato novo: crie `xxx_reader.py` com uma subclasse de
`LeitorArquivo` (defina `formatos = (...)`) e registre a instância em `_LEITORES`.
"""
from __future__ import annotations

from app.readers.base import DocumentoIlegivel, ErroLeitura, LeitorArquivo
from app.readers.deteccao import FormatoNaoSuportado, detectar_formato
from app.readers.docx_reader import DocxReader
from app.readers.image_reader import ImageReader
from app.readers.pdf_reader import PdfReader
from app.readers.txt_reader import TxtReader
from app.readers.xlsx_reader import XlsxReader

_LEITORES: list[LeitorArquivo] = [
    PdfReader(),
    DocxReader(),
    XlsxReader(),
    ImageReader(),
    TxtReader(),
]

_POR_FORMATO: dict[str, LeitorArquivo] = {
    fmt: leitor for leitor in _LEITORES for fmt in leitor.formatos
}


def obter_leitor(formato: str) -> LeitorArquivo:
    try:
        return _POR_FORMATO[formato]
    except KeyError:
        raise FormatoNaoSuportado(f"Sem leitor para o formato '{formato}'.")


__all__ = [
    "obter_leitor",
    "detectar_formato",
    "FormatoNaoSuportado",
    "ErroLeitura",
    "DocumentoIlegivel",
]
