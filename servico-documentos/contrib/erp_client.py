"""ErpClient — REFERÊNCIA, não faz parte do serviço stateless.

Decisão de arquitetura (ver docs/modulo-ia-documentos.md): a CONFIRMAÇÃO
human-in-the-loop e o ENVIO AO ERP ficam no backend .NET, não aqui. Este
arquivo é uma implementação de referência do cliente HTTP para quem for
implementar/depurar esse envio — em .NET (`HttpClient` + `Polly`) ou num
eventual worker Python.

Cobre o que o prompt pediu para a etapa 3:
  - autenticação OAuth2 client_credentials (com cache/refresh de token)
  - timeout, retry com backoff exponencial + jitter
  - tratamento de 2xx / 4xx (validação) / 5xx (servidor)
  - idempotência (Idempotency-Key por lote)
  - log de auditoria (quem confirmou, quando, original vs. corrigido, payload, resposta)

Ajuste os pontos marcados com  # >>> AJUSTAR.
"""
from __future__ import annotations

import json
import logging
import random
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any

import requests

log = logging.getLogger("erp_client")
audit = logging.getLogger("erp_client.auditoria")


# --------------------------------------------------------------------------- #
# Configuração                                                                #
# --------------------------------------------------------------------------- #
@dataclass
class ErpConfig:
    base_url: str                       # >>> AJUSTAR: ex. "https://erp.empresa.com/api"
    token_url: str                      # >>> AJUSTAR: endpoint OAuth2 de token
    client_id: str
    client_secret: str
    scope: str = ""
    timeout_segundos: float = 30.0
    max_tentativas: int = 4             # 1 tentativa + 3 retries
    backoff_base_segundos: float = 0.5  # 0.5, 1, 2, 4 ... + jitter

    # >>> AJUSTAR: rota no ERP por tipo de documento.
    # Deve casar com `SchemaDocumento.endpoint_erp` em app/schemas/tipos.py.
    rotas_por_tipo: dict[str, str] = field(default_factory=lambda: {
        "NotaFiscal": "/notas-fiscais",
        "Boleto": "/boletos",
        "PedidoCompra": "/pedidos",
        "Contrato": "/contratos",
        "ComprovantePagamento": "/despesas/comprovante",
    })


# --------------------------------------------------------------------------- #
# Exceções                                                                    #
# --------------------------------------------------------------------------- #
class ErpError(Exception):
    pass


class ErpAuthError(ErpError):
    pass


class ErpValidacaoError(ErpError):
    """4xx — payload rejeitado pelo ERP. NÃO adianta repetir sem corrigir."""
    def __init__(self, status: int, corpo: Any):
        super().__init__(f"ERP recusou o documento (HTTP {status}): {corpo}")
        self.status = status
        self.corpo = corpo


class ErpIndisponivelError(ErpError):
    """5xx / rede / timeout — repetível."""


# --------------------------------------------------------------------------- #
# Cliente                                                                     #
# --------------------------------------------------------------------------- #
class ErpClient:
    def __init__(self, cfg: ErpConfig, session: requests.Session | None = None):
        self.cfg = cfg
        self._s = session or requests.Session()
        self._token: str | None = None
        self._token_exp: float = 0.0

    # ---- auth -------------------------------------------------------------- #
    def _obter_token(self) -> str:
        agora = time.time()
        if self._token and agora < self._token_exp - 30:
            return self._token
        try:
            r = self._s.post(
                self.cfg.token_url,
                data={
                    "grant_type": "client_credentials",
                    "client_id": self.cfg.client_id,
                    "client_secret": self.cfg.client_secret,
                    **({"scope": self.cfg.scope} if self.cfg.scope else {}),
                },
                timeout=self.cfg.timeout_segundos,
            )
        except requests.RequestException as e:
            raise ErpAuthError(f"Falha ao contatar o endpoint de token: {e}") from e
        if r.status_code != 200:
            raise ErpAuthError(f"OAuth2 retornou HTTP {r.status_code}: {r.text[:300]}")
        dados = r.json()
        self._token = dados["access_token"]
        self._token_exp = agora + float(dados.get("expires_in", 3600))
        log.info("Token OAuth2 renovado (expira em %ss).", dados.get("expires_in", "?"))
        return self._token

    # ---- request com retry ---------------------------------------------------
    def _request(self, metodo: str, url: str, *, json_body: dict, headers: dict) -> requests.Response:
        ultima_exc: Exception | None = None
        for tentativa in range(1, self.cfg.max_tentativas + 1):
            try:
                resp = self._s.request(
                    metodo, url, json=json_body, headers=headers,
                    timeout=self.cfg.timeout_segundos,
                )
            except requests.RequestException as e:
                ultima_exc = e
                log.warning("Tentativa %d/%d falhou (rede): %s",
                            tentativa, self.cfg.max_tentativas, e)
            else:
                if resp.status_code < 400:
                    return resp
                if 400 <= resp.status_code < 500 and resp.status_code not in (408, 429):
                    return resp  # erro de validação — devolve, quem chama trata
                # 5xx / 408 / 429 -> repetível
                ultima_exc = ErpIndisponivelError(
                    f"HTTP {resp.status_code}: {resp.text[:300]}"
                )
                log.warning("Tentativa %d/%d: HTTP %d (repetível)",
                            tentativa, self.cfg.max_tentativas, resp.status_code)

            if tentativa < self.cfg.max_tentativas:
                espera = self.cfg.backoff_base_segundos * (2 ** (tentativa - 1))
                espera += random.uniform(0, espera * 0.25)  # jitter
                time.sleep(espera)

        raise ErpIndisponivelError(f"ERP indisponível após {self.cfg.max_tentativas} tentativas: {ultima_exc}")

    # ---- API pública ------------------------------------------------------- #
    def enviar_documento(
        self,
        tipo: str,
        payload: dict,
        *,
        lote_id: str,
        auditoria: "RegistroAuditoria | None" = None,
    ) -> dict:
        """Envia UM documento já confirmado ao ERP.

        `lote_id` é usado como chave de idempotência: reenviar o mesmo lote não
        cria duplicado (o ERP deve tratar a Idempotency-Key). # >>> AJUSTAR ao ERP.
        """
        rota = self.cfg.rotas_por_tipo.get(tipo)
        if not rota:
            raise ErpError(f"Sem rota configurada no ERP para o tipo '{tipo}'.")
        url = self.cfg.base_url.rstrip("/") + rota

        headers = {
            "Authorization": f"Bearer {self._obter_token()}",
            "Content-Type": "application/json",
            "Idempotency-Key": lote_id,          # >>> AJUSTAR: header que o ERP espera
            "X-Origem": "servico-documentos",
        }

        log.info("[%s] POST %s", lote_id, url)
        resp = self._request("POST", url, json_body=payload, headers=headers)

        try:
            corpo = resp.json()
        except ValueError:
            corpo = {"raw": resp.text[:500]}

        if auditoria is not None:
            auditoria.payload_enviado = payload
            auditoria.resposta_erp = {"status": resp.status_code, "corpo": corpo}
            auditoria.registrar()

        if resp.status_code in (200, 201, 202):
            log.info("[%s] ERP aceitou (HTTP %d).", lote_id, resp.status_code)
            return corpo
        if resp.status_code == 409:
            # idempotência: lote já enviado antes — trata como sucesso
            log.info("[%s] lote já existia no ERP (409) — idempotente.", lote_id)
            return corpo
        if 400 <= resp.status_code < 500:
            raise ErpValidacaoError(resp.status_code, corpo)
        raise ErpIndisponivelError(f"HTTP {resp.status_code}: {corpo}")


# --------------------------------------------------------------------------- #
# Auditoria                                                                   #
# --------------------------------------------------------------------------- #
@dataclass
class RegistroAuditoria:
    lote_id: str
    tipo_documento: str
    confirmado_por: str                     # usuário Administrador que revisou
    confirmado_em: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    dados_originais: dict = field(default_factory=dict)   # o que a IA extraiu
    dados_corrigidos: dict = field(default_factory=dict)  # o que o humano confirmou
    payload_enviado: dict | None = None
    resposta_erp: dict | None = None

    @property
    def campos_alterados(self) -> dict[str, dict]:
        alt = {}
        for k, novo in self.dados_corrigidos.items():
            antigo = self.dados_originais.get(k)
            if antigo != novo:
                alt[k] = {"de": antigo, "para": novo}
        return alt

    def registrar(self) -> None:
        # >>> AJUSTAR: no ERP real isto vira uma linha na tabela de Log de
        # Auditoria (a mesma usada pelos lançamentos manuais).
        audit.info(json.dumps({
            "loteId": self.lote_id,
            "tipoDocumento": self.tipo_documento,
            "confirmadoPor": self.confirmado_por,
            "confirmadoEm": self.confirmado_em,
            "camposAlterados": self.campos_alterados,
            "payloadEnviado": self.payload_enviado,
            "respostaErp": self.resposta_erp,
        }, ensure_ascii=False))
