"""Logging estruturado simples. Cada lote carrega o mesmo `loteId` nos logs
para rastreabilidade ponta a ponta com o backend .NET."""
from __future__ import annotations

import logging
import sys

_configurado = False


def configurar_logging(nivel: str = "INFO") -> None:
    global _configurado
    if _configurado:
        return
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(
        logging.Formatter(
            "%(asctime)s %(levelname)-7s [%(name)s] %(message)s",
            datefmt="%Y-%m-%dT%H:%M:%S",
        )
    )
    root = logging.getLogger()
    root.handlers.clear()
    root.addHandler(handler)
    root.setLevel(nivel.upper())
    # bibliotecas barulhentas
    logging.getLogger("pdfminer").setLevel(logging.WARNING)
    logging.getLogger("PIL").setLevel(logging.WARNING)
    _configurado = True


def get_logger(nome: str) -> logging.Logger:
    return logging.getLogger(nome)
