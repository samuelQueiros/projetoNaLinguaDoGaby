"""Interface comum de leitura por formato de arquivo. Plugável: cada formato
é uma subclasse registrada em `app/readers/__init__.py`."""
from __future__ import annotations

import abc

from app.models import DocumentoBruto


class ErroLeitura(Exception):
    """Falha ao ler o arquivo (corrompido, formato inesperado, lib ausente)."""


class DocumentoIlegivel(ErroLeitura):
    """Arquivo lido mas sem texto aproveitável -> vira HTTP 422 no /extrair."""


class LeitorArquivo(abc.ABC):
    #: formatos (extensão sem ponto) que este leitor cobre
    formatos: tuple[str, ...] = ()

    @abc.abstractmethod
    def ler(self, dados: bytes, nome_arquivo: str) -> DocumentoBruto:
        """Extrai texto/estrutura. Deve levantar `DocumentoIlegivel` se não
        houver texto utilizável (mesmo depois do OCR)."""
        raise NotImplementedError


def _normalizar_texto(texto: str) -> str:
    """Colapsa espaços/linhas em branco excessivos sem perder quebras
    significativas — ajuda regex e reduz tokens no LLM."""
    linhas = [ln.rstrip() for ln in texto.replace("\r\n", "\n").split("\n")]
    saida: list[str] = []
    brancas = 0
    for ln in linhas:
        if ln.strip():
            brancas = 0
            saida.append(" ".join(ln.split()))
        else:
            brancas += 1
            if brancas <= 1:
                saida.append("")
    return "\n".join(saida).strip()
