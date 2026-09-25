from __future__ import annotations

from app.models import DocumentoBruto
from app.readers.base import DocumentoIlegivel, LeitorArquivo, _normalizar_texto


class TxtReader(LeitorArquivo):
    formatos = ("txt",)

    def ler(self, dados: bytes, nome_arquivo: str) -> DocumentoBruto:
        for enc in ("utf-8", "latin-1", "cp1252"):
            try:
                texto = dados.decode(enc)
                break
            except UnicodeDecodeError:
                continue
        else:
            raise DocumentoIlegivel("Não consegui decodificar o arquivo de texto.")

        texto = _normalizar_texto(texto)
        if not texto:
            raise DocumentoIlegivel("Arquivo de texto vazio.")
        return DocumentoBruto(texto=texto, paginas=1, metadados={"encoding": enc})
