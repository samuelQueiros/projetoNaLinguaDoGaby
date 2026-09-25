"""Camada de CONFIRMAÇÃO (human-in-the-loop) — REFERÊNCIA.

No ERP este papel é do backend .NET (`DocumentoImportado` + fila +
`GerenciadorDocumentosImportados`, revisão só-Admin). Este módulo é uma
implementação mínima equivalente, com persistência em SQLite, para permitir
rodar o exemplo ponta-a-ponta (`exemplo_uso.py`) sem subir o .NET.

Estados do lote:  pendente -> confirmado -> enviado   (ou -> rejeitado)
"""
from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

from app.models import RespostaExtracao

_DB = Path(__file__).parent / "lotes.sqlite3"


def _conn() -> sqlite3.Connection:
    c = sqlite3.connect(_DB)
    c.execute("""
        CREATE TABLE IF NOT EXISTS lote (
            lote_id        TEXT PRIMARY KEY,
            tipo           TEXT NOT NULL,
            status         TEXT NOT NULL,
            extracao_json  TEXT NOT NULL,      -- RespostaExtracao (dados originais da IA)
            corrigido_json TEXT,               -- dados após revisão humana
            confirmado_por TEXT,
            confirmado_em  TEXT,
            enviado_em     TEXT,
            resposta_erp   TEXT
        )
    """)
    return c


def salvar_extracao(resp: RespostaExtracao) -> str:
    """Persiste o resultado da extração como lote 'pendente'. Retorna o loteId."""
    with _conn() as c:
        c.execute(
            "INSERT OR REPLACE INTO lote (lote_id, tipo, status, extracao_json) VALUES (?,?,?,?)",
            (resp.lote_id, resp.tipo_detectado.value, "pendente",
             resp.model_dump_json(by_alias=True)),
        )
    return resp.lote_id


def obter_lote(lote_id: str) -> dict:
    with _conn() as c:
        row = c.execute("SELECT * FROM lote WHERE lote_id = ?", (lote_id,)).fetchone()
        if not row:
            raise KeyError(f"Lote {lote_id} não encontrado.")
        cols = [d[0] for d in c.execute("SELECT * FROM lote WHERE lote_id = ?", (lote_id,)).description]
    return dict(zip(cols, row))


class LoteJaProcessado(Exception):
    pass


def confirmar_dados(id_lote: str, dados_corrigidos: dict, confirmado_por: str) -> dict:
    """Recebe os dados JÁ revisados/corrigidos pelo usuário (mapa
    campo -> valor) e move o lote para 'confirmado'. NÃO envia ao ERP —
    isso é passo separado (`exemplo_uso.marcar_enviado`)."""
    lote = obter_lote(id_lote)
    if lote["status"] != "pendente":
        raise LoteJaProcessado(f"Lote {id_lote} está '{lote['status']}', não 'pendente'.")

    extracao = json.loads(lote["extracao_json"])
    obrig_pendentes = [
        nome for nome in extracao["camposObrigatoriosPendentes"]
        if not dados_corrigidos.get(nome)
    ]
    if obrig_pendentes:
        raise ValueError(f"Campos obrigatórios ainda vazios: {obrig_pendentes}")

    with _conn() as c:
        c.execute(
            "UPDATE lote SET status='confirmado', corrigido_json=?, confirmado_por=?, confirmado_em=? "
            "WHERE lote_id=?",
            (json.dumps(dados_corrigidos, ensure_ascii=False), confirmado_por,
             datetime.now(timezone.utc).isoformat(), id_lote),
        )
    return obter_lote(id_lote)


def rejeitar(id_lote: str, motivo: str) -> None:
    with _conn() as c:
        c.execute("UPDATE lote SET status='rejeitado', resposta_erp=? WHERE lote_id=?",
                  (json.dumps({"motivo": motivo}), id_lote))


def marcar_enviado(id_lote: str, resposta_erp: dict) -> None:
    with _conn() as c:
        c.execute(
            "UPDATE lote SET status='enviado', enviado_em=?, resposta_erp=? WHERE lote_id=?",
            (datetime.now(timezone.utc).isoformat(),
             json.dumps(resposta_erp, ensure_ascii=False), id_lote),
        )


def ja_enviado(id_lote: str) -> bool:
    """Guarda de idempotência antes de chamar o ERP."""
    return obter_lote(id_lote)["status"] == "enviado"
