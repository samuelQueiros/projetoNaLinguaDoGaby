"""Exemplo ponta a ponta:  leitura -> revisão -> confirmação -> envio ao ERP.

Roda offline (LLM_PROVIDER=none) e com um ERP FALSO em memória — não precisa de
rede nem do backend .NET. Serve para ver o formato de cada etapa.

    python exemplo_uso.py

As etapas 2 e 3 usam as implementações de REFERÊNCIA em `contrib/` (no
produto elas são do backend .NET).
"""
from __future__ import annotations

import json
import logging

from app.logging_config import configurar_logging
from app.pipeline import processar
from contrib import confirmacao
from contrib.erp_client import ErpClient, ErpConfig, ErpValidacaoError, RegistroAuditoria

configurar_logging("INFO")

# --------------------------------------------------------------------------- #
# Documento de teste (uma NF-e em texto — como sairia de um PDF nativo)       #
# --------------------------------------------------------------------------- #
NOTA_FISCAL_TXT = """\
DANFE - DOCUMENTO AUXILIAR DA NOTA FISCAL ELETRONICA
NF-e Nº 000.123.456   SÉRIE 1
NATUREZA DA OPERAÇÃO: PRESTACAO DE SERVICO
CHAVE DE ACESSO 3526 0912 3456 7800 0199 5500 1000 0012 3411 2233 4455

EMITENTE
Razão Social: ELETRICA SILVA E FILHOS LTDA
CNPJ: 12.345.678/0001-99
Endereço: Rua das Palmeiras, 100 - Sao Paulo/SP

DATA DE EMISSÃO: 15/08/2026
DATA DE VENCIMENTO: 14/09/2026

VALOR TOTAL DA NOTA R$ 1.530,00
ICMS: 0,00
"""


# --------------------------------------------------------------------------- #
# ERP falso: implementa só o que o ErpClient chama (.post / .request)         #
# --------------------------------------------------------------------------- #
class _Resp:
    def __init__(self, status, body):
        self.status_code = status
        self._body = body
        self.text = json.dumps(body)

    def json(self):
        return self._body


class FakeErpSession:
    """Aceita 1 vez por Idempotency-Key; repetição -> 409 (idempotente)."""
    def __init__(self):
        self.recebidos: dict[str, dict] = {}

    def post(self, url, data=None, timeout=None, **kw):  # endpoint de token OAuth2
        return _Resp(200, {"access_token": "tok-abc", "expires_in": 3600})

    def request(self, metodo, url, json=None, headers=None, timeout=None, **kw):
        chave = headers.get("Idempotency-Key")
        # valida um campo obrigatório para demonstrar o caminho 422
        if not json.get("valor"):
            return _Resp(422, {"erro": "valor é obrigatório", "campo": "valor"})
        if chave in self.recebidos:
            return _Resp(409, {"id": self.recebidos[chave]["id"], "status": "ja_existia"})
        novo = {"id": f"NF-{len(self.recebidos) + 1}", "status": "registrado"}
        self.recebidos[chave] = novo
        return _Resp(201, novo)


# --------------------------------------------------------------------------- #
# Mapeia os campos canônicos -> payload esperado pelo ERP, por tipo.          #
# >>> AJUSTAR ao contrato real de cada endpoint do ERP.                       #
# --------------------------------------------------------------------------- #
def montar_payload(tipo: str, dados: dict) -> dict:
    if tipo == "NotaFiscal":
        return {
            "numero": dados.get("numeroNota"),
            "fornecedorCnpj": dados.get("cnpj"),
            "fornecedorNome": dados.get("fornecedor"),
            "valor": dados.get("valor"),           # string decimal "1530.00"
            "dataEmissao": dados.get("data"),
            "dataVencimento": dados.get("vencimento"),
        }
    raise ValueError(f"Sem mapeamento de payload para o tipo {tipo}")


def main() -> None:
    print("\n=== ETAPA 1 — EXTRAÇÃO =========================================")
    resp = processar(NOTA_FISCAL_TXT.encode("utf-8"), "nf-123.txt")
    print(resp.model_dump_json(by_alias=True, indent=2))

    lote_id = confirmacao.salvar_extracao(resp)
    print(f"\nLote {lote_id} salvo como 'pendente'.")

    print("\n=== ETAPA 2 — CONFIRMAÇÃO (human-in-the-loop) ==================")
    # o revisor (Administrador) vê os campos e corrige o que precisar.
    # Aqui: a IA leu o fornecedor com ruído; o humano ajusta e confirma.
    dados_originais = {c.nome: c.valor for c in resp.campos}
    dados_corrigidos = dict(dados_originais)
    dados_corrigidos["fornecedor"] = "Elétrica Silva e Filhos Ltda"  # correção manual
    print("originais :", dados_originais)
    print("corrigidos:", dados_corrigidos)

    confirmacao.confirmar_dados(lote_id, dados_corrigidos, confirmado_por="ana.admin")
    print(f"Lote {lote_id} -> 'confirmado'.")

    print("\n=== ETAPA 3 — INTEGRAÇÃO COM O ERP =============================")
    if confirmacao.ja_enviado(lote_id):
        print("Lote já enviado — nada a fazer (idempotência).")
        return

    cfg = ErpConfig(
        base_url="https://erp.exemplo.local/api",
        token_url="https://erp.exemplo.local/oauth/token",
        client_id="servico-documentos",
        client_secret="troque-por-secret-real",   # >>> AJUSTAR (via env/secret)
    )
    cliente = ErpClient(cfg, session=FakeErpSession())

    tipo = resp.tipo_detectado.value
    payload = montar_payload(tipo, dados_corrigidos)

    aud = RegistroAuditoria(
        lote_id=lote_id, tipo_documento=tipo, confirmado_por="ana.admin",
        dados_originais=dados_originais, dados_corrigidos=dados_corrigidos,
    )

    try:
        resultado = cliente.enviar_documento(tipo, payload, lote_id=lote_id, auditoria=aud)
        confirmacao.marcar_enviado(lote_id, resultado)
        print("ERP respondeu:", resultado)
    except ErpValidacaoError as e:
        confirmacao.rejeitar(lote_id, f"ERP recusou: {e.corpo}")
        logging.error("Envio recusado pelo ERP (4xx): %s", e)
        return

    print("\n=== REENVIO DO MESMO LOTE (idempotência) =======================")
    r2 = cliente.enviar_documento(tipo, payload, lote_id=lote_id)
    print("2ª chamada (deve ser idempotente):", r2)

    print("\n=== ESTADO FINAL DO LOTE ======================================")
    print(json.dumps(confirmacao.obter_lote(lote_id), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
