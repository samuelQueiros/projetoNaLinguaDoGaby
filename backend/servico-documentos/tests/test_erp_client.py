"""Testes do ErpClient de referência (contrib/) — retry, 4xx/5xx, idempotência."""
from __future__ import annotations

import pytest

from contrib.erp_client import (
    ErpClient,
    ErpConfig,
    ErpIndisponivelError,
    ErpValidacaoError,
)


class _Resp:
    def __init__(self, status, body):
        self.status_code = status
        self._body = body
        self.text = str(body)

    def json(self):
        return self._body


class FakeSession:
    def __init__(self, respostas):
        self._respostas = list(respostas)
        self.chamadas = 0

    def post(self, *a, **k):
        return _Resp(200, {"access_token": "t", "expires_in": 3600})

    def request(self, *a, **k):
        self.chamadas += 1
        r = self._respostas.pop(0)
        if isinstance(r, Exception):
            raise r
        return r


def _cfg():
    return ErpConfig(base_url="http://erp", token_url="http://erp/tok",
                     client_id="c", client_secret="s",
                     backoff_base_segundos=0.0, max_tentativas=3)


def test_sucesso_201():
    s = FakeSession([_Resp(201, {"id": "X1"})])
    cli = ErpClient(_cfg(), session=s)
    out = cli.enviar_documento("NotaFiscal", {"valor": "1"}, lote_id="L1")
    assert out == {"id": "X1"}


def test_5xx_faz_retry_e_depois_sucede():
    s = FakeSession([_Resp(500, "boom"), _Resp(503, "again"), _Resp(200, {"id": "X"})])
    cli = ErpClient(_cfg(), session=s)
    out = cli.enviar_documento("NotaFiscal", {"valor": "1"}, lote_id="L1")
    assert out == {"id": "X"}
    assert s.chamadas == 3


def test_5xx_persistente_levanta_indisponivel():
    s = FakeSession([_Resp(500, "x")] * 3)
    cli = ErpClient(_cfg(), session=s)
    with pytest.raises(ErpIndisponivelError):
        cli.enviar_documento("NotaFiscal", {"valor": "1"}, lote_id="L1")


def test_4xx_nao_faz_retry():
    s = FakeSession([_Resp(422, {"erro": "valor invalido"})])
    cli = ErpClient(_cfg(), session=s)
    with pytest.raises(ErpValidacaoError):
        cli.enviar_documento("NotaFiscal", {"valor": ""}, lote_id="L1")
    assert s.chamadas == 1


def test_409_e_tratado_como_idempotente():
    s = FakeSession([_Resp(409, {"id": "ja"})])
    cli = ErpClient(_cfg(), session=s)
    out = cli.enviar_documento("NotaFiscal", {"valor": "1"}, lote_id="L1")
    assert out == {"id": "ja"}


def test_tipo_sem_rota_configurada():
    cli = ErpClient(_cfg(), session=FakeSession([]))
    with pytest.raises(Exception):
        cli.enviar_documento("TipoInexistente", {}, lote_id="L1")
