"""Testes da API HTTP (app/main.py) — autenticação entre serviços e o
contrato de status codes documentado no docstring do módulo.
"""
from __future__ import annotations

from fastapi.testclient import TestClient

import app.main as main_module
from app.main import app

client = TestClient(app)


def _arquivo_txt(nome: str = "nota.txt", conteudo: bytes = b"conteudo de teste") -> dict:
    return {"arquivo": (nome, conteudo, "text/plain")}


def test_sem_internal_token_configurado_extrair_funciona_sem_header():
    # Padrão em dev: INTERNAL_TOKEN vazio = checagem desativada.
    main_module.cfg.internal_token = ""

    resposta = client.post("/extrair", files=_arquivo_txt())

    assert resposta.status_code == 200


def test_com_internal_token_configurado_sem_header_e_recusado():
    main_module.cfg.internal_token = "segredo-teste"
    try:
        resposta = client.post("/extrair", files=_arquivo_txt())
        assert resposta.status_code == 401
    finally:
        main_module.cfg.internal_token = ""


def test_com_internal_token_configurado_header_errado_e_recusado():
    main_module.cfg.internal_token = "segredo-teste"
    try:
        resposta = client.post(
            "/extrair", files=_arquivo_txt(), headers={"X-Internal-Token": "errado"}
        )
        assert resposta.status_code == 401
    finally:
        main_module.cfg.internal_token = ""


def test_com_internal_token_configurado_header_correto_funciona():
    main_module.cfg.internal_token = "segredo-teste"
    try:
        resposta = client.post(
            "/extrair", files=_arquivo_txt(), headers={"X-Internal-Token": "segredo-teste"}
        )
        assert resposta.status_code == 200
    finally:
        main_module.cfg.internal_token = ""


def test_arquivo_maior_que_o_limite_e_recusado_com_413():
    # Verifica o comportamento (413), não a implementação — mas é a mesma
    # checagem que antes só rodava depois de ler o arquivo inteiro pra
    # memória (achado da auditoria de segurança corrigido em
    # app/main.py::_ler_com_limite, que lê em blocos e aborta cedo).
    main_module.cfg.max_arquivo_mb = 1
    try:
        grande = b"x" * (2 * 1024 * 1024)
        resposta = client.post("/extrair", files=_arquivo_txt(conteudo=grande))
        assert resposta.status_code == 413
    finally:
        main_module.cfg.max_arquivo_mb = 25


def test_falha_interna_no_processamento_nao_vaza_detalhe_da_excecao():
    from unittest.mock import patch

    from app.pipeline import ErroExtracao

    with patch("app.main.processar", side_effect=ErroExtracao("caminho interno: /var/lib/segredo/arquivo.tmp")):
        resposta = client.post("/extrair", files=_arquivo_txt())

    assert resposta.status_code == 500
    assert "/var/lib/segredo" not in resposta.text
    assert "caminho interno" not in resposta.text
