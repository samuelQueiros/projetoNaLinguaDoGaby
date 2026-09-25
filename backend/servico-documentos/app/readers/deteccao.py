"""Detecção automática do tipo de ARQUIVO (não do tipo de documento).

Estratégia: magic bytes primeiro (confiável), extensão como desempate/fallback.
"""
from __future__ import annotations

import os

# assinaturas de magic bytes -> formato canônico
_ASSINATURAS: list[tuple[bytes, str]] = [
    (b"%PDF-", "pdf"),
    (b"\x89PNG\r\n\x1a\n", "png"),
    (b"\xff\xd8\xff", "jpg"),
    (b"PK\x03\x04", "zip"),  # docx/xlsx são zip — refina abaixo
    (b"II*\x00", "tiff"),
    (b"MM\x00*", "tiff"),
]

_EXT_PARA_FORMATO = {
    ".pdf": "pdf",
    ".docx": "docx",
    ".doc": "docx",
    ".xlsx": "xlsx",
    ".xls": "xlsx",
    ".jpg": "jpg",
    ".jpeg": "jpg",
    ".png": "png",
    ".txt": "txt",
    ".text": "txt",
    ".csv": "txt",
    ".tif": "tiff",
    ".tiff": "tiff",
}

FORMATOS_SUPORTADOS = {"pdf", "docx", "xlsx", "jpg", "png", "txt", "tiff"}


class FormatoNaoSuportado(Exception):
    pass


def _refinar_zip(dados: bytes, ext_formato: str | None) -> str:
    """docx e xlsx são ambos ZIP; diferencia pelo conteúdo do pacote OOXML."""
    amostra = dados[:4000]
    if b"word/" in amostra:
        return "docx"
    if b"xl/" in amostra:
        return "xlsx"
    return ext_formato or "docx"


def detectar_formato(dados: bytes, nome_arquivo: str) -> str:
    ext = os.path.splitext(nome_arquivo or "")[1].lower()
    ext_formato = _EXT_PARA_FORMATO.get(ext)

    magic_formato: str | None = None
    for assinatura, fmt in _ASSINATURAS:
        if dados.startswith(assinatura):
            magic_formato = fmt
            break

    if magic_formato == "zip":
        formato = _refinar_zip(dados, ext_formato)
    elif magic_formato:
        formato = magic_formato
    elif ext_formato:
        formato = ext_formato
    else:
        # TXT não tem magic byte: se decodifica como utf-8/latin-1, tratamos como txt
        try:
            dados[:2048].decode("utf-8")
            formato = "txt"
        except UnicodeDecodeError:
            try:
                dados[:2048].decode("latin-1")
                formato = "txt"
            except UnicodeDecodeError:
                raise FormatoNaoSuportado(
                    f"Não reconheci o formato de '{nome_arquivo}'."
                )

    if formato == "tiff":
        formato = "png"  # readers de imagem tratam via Pillow
    if formato not in FORMATOS_SUPORTADOS and formato != "png":
        raise FormatoNaoSuportado(f"Formato '{formato}' não suportado.")
    return formato
