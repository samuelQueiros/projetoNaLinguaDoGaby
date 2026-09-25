"""Testes do módulo de extração por LLM — offline, sem chamada real a
nenhum provider (monkeypatch no dispatch table `_PROVIDERS`).
"""
from __future__ import annotations

import pytest

from app.config import Config, get_config
from app.extracao import llm_extractor
from app.models import CampoExtraido, StatusCampo, TipoDocumento
from app.schemas import obter_schema

SCHEMA = obter_schema(TipoDocumento.COMPROVANTE_PAGAMENTO)


def _cfg(**overrides) -> Config:
    base = dict(llm_provider="gemini", llm_api_key="chave-teste", llm_model="gemini-2.5-flash")
    base.update(overrides)
    return Config(**base)


def test_gemini_esta_no_dispatch_table():
    assert "gemini" in llm_extractor._PROVIDERS
    assert "anthropic" in llm_extractor._PROVIDERS
    assert "openai" in llm_extractor._PROVIDERS


def test_extrair_por_llm_usa_cfg_override_em_vez_do_global(monkeypatch):
    chamadas: list[str] = []

    def fake_gemini(sistema: str, usuario: str, cfg, schema) -> str:
        chamadas.append(cfg.llm_provider)
        return '{"valor": {"valor": "150.00", "confianca": 0.95, "origem": "linha 3"}}'

    monkeypatch.setitem(llm_extractor._PROVIDERS, "gemini", fake_gemini)

    def get_config_nao_deveria_ser_chamado():
        raise AssertionError("extrair_por_llm não deveria chamar get_config() quando recebe cfg override")

    monkeypatch.setattr(llm_extractor, "get_config", get_config_nao_deveria_ser_chamado)

    resultado = llm_extractor.extrair_por_llm("texto qualquer", SCHEMA, cfg=_cfg())

    assert chamadas == ["gemini"]
    assert resultado["valor"].valor == "150.00"
    assert resultado["valor"].confianca == 0.95


def test_extrair_por_llm_sem_cfg_cai_no_global(monkeypatch):
    chamado = {"vezes": 0}

    def get_config_falsa():
        chamado["vezes"] += 1
        return Config(llm_provider="none")

    monkeypatch.setattr(llm_extractor, "get_config", get_config_falsa)

    resultado = llm_extractor.extrair_por_llm("texto qualquer", SCHEMA)

    assert chamado["vezes"] == 1
    assert resultado == {}  # llm_provider=none -> usa_llm é False, retorna vazio


def test_provider_desconhecido_levanta_erro_llm(monkeypatch):
    # openai é válido no Config, mas simulamos que o dispatch não conhece
    # nenhum provider ainda (ex.: lib não instalada e removida do catálogo).
    monkeypatch.setattr(llm_extractor, "_PROVIDERS", {})

    with pytest.raises(llm_extractor.ErroLLM):
        llm_extractor.extrair_por_llm("texto", SCHEMA, cfg=_cfg(llm_provider="openai"))


def test_falha_transitoria_tenta_de_novo_e_no_fim_funciona(monkeypatch):
    # Rate limit/erro 5xx passageiro é o caso mais comum de falha real de
    # LLM — antes, a primeira falha já derrubava pro fallback regex-only
    # sem tentar de novo (achado da auditoria de qualidade).
    tentativas: list[int] = []

    def fake_instavel(sistema, usuario, cfg, schema) -> str:
        tentativas.append(1)
        if len(tentativas) == 1:
            raise RuntimeError("HTTP 429: rate limited")
        return '{"valor": {"valor": "150.00", "confianca": 0.9, "origem": "x"}}'

    monkeypatch.setitem(llm_extractor._PROVIDERS, "gemini", fake_instavel)
    monkeypatch.setattr(llm_extractor.time, "sleep", lambda segundos: None)  # sem esperar de verdade no teste

    resultado = llm_extractor.extrair_por_llm("texto", SCHEMA, cfg=_cfg(llm_max_tentativas=2))

    assert len(tentativas) == 2
    assert resultado["valor"].valor == "150.00"


def test_falha_persistente_desiste_apos_max_tentativas(monkeypatch):
    tentativas: list[int] = []

    def fake_sempre_falha(sistema, usuario, cfg, schema) -> str:
        tentativas.append(1)
        raise RuntimeError("HTTP 500")

    monkeypatch.setitem(llm_extractor._PROVIDERS, "gemini", fake_sempre_falha)
    monkeypatch.setattr(llm_extractor.time, "sleep", lambda segundos: None)

    with pytest.raises(llm_extractor.ErroLLM):
        llm_extractor.extrair_por_llm("texto", SCHEMA, cfg=_cfg(llm_max_tentativas=3))

    assert len(tentativas) == 3


def test_lib_nao_instalada_nao_tenta_de_novo(monkeypatch):
    tentativas: list[int] = []

    def fake_sem_lib(sistema, usuario, cfg, schema) -> str:
        tentativas.append(1)
        raise ImportError("lib não instalada")

    monkeypatch.setitem(llm_extractor._PROVIDERS, "gemini", fake_sem_lib)

    with pytest.raises(llm_extractor.ErroLLM):
        llm_extractor.extrair_por_llm("texto", SCHEMA, cfg=_cfg(llm_max_tentativas=3))

    assert len(tentativas) == 1  # ImportError nunca é repetível


def test_montar_prompt_inclui_candidatos_do_regex_como_grounding():
    candidatos = {
        "valor": CampoExtraido(nome="valor", valor="150.00", confianca=0.75, origem="Valor: R$ 150,00"),
    }
    _, usuario = llm_extractor._montar_prompt("texto qualquer", SCHEMA, candidatos)

    assert 'candidato "150.00"' in usuario
    assert "Valor: R$ 150,00" in usuario
    assert "75%" in usuario


def test_montar_prompt_sem_candidatos_indica_isso_explicitamente():
    _, usuario = llm_extractor._montar_prompt("texto qualquer", SCHEMA, None)
    assert "nenhum candidato pré-encontrado" in usuario


def test_prompt_de_sistema_tem_calibracao_de_confianca_e_regra_de_nao_inventar():
    sistema, _ = llm_extractor._montar_prompt("texto", SCHEMA)
    assert "CALIBRAÇÃO DE CONFIANÇA" in sistema
    assert "NUNCA invente" in sistema
    assert "OCR" in sistema


def test_extrair_por_llm_passa_candidatos_para_o_provider(monkeypatch):
    prompts_recebidos = []

    def fake_gemini(sistema: str, usuario: str, cfg, schema) -> str:
        prompts_recebidos.append(usuario)
        return '{"valor": {"valor": null, "confianca": 0, "origem": null}}'

    monkeypatch.setitem(llm_extractor._PROVIDERS, "gemini", fake_gemini)
    candidatos = {"valor": CampoExtraido(nome="valor", valor="99.90", confianca=0.6, origem="Total: 99,90")}

    llm_extractor.extrair_por_llm("texto", SCHEMA, cfg=_cfg(), campos_regex=candidatos)

    assert any("99.90" in p for p in prompts_recebidos)


def test_schema_json_llm_tem_todos_os_campos_como_obrigatorios_no_shape():
    schema_json = llm_extractor._schema_json_llm(SCHEMA)

    assert schema_json["type"] == "object"
    nomes_campos = {c.nome for c in SCHEMA.campos}
    assert set(schema_json["properties"].keys()) == nomes_campos
    assert set(schema_json["required"]) == nomes_campos
    # cada campo tem o "objeto de resposta" com valor/confianca/origem
    exemplo = schema_json["properties"]["valor"]
    assert set(exemplo["required"]) == {"valor", "confianca"}
    assert "confianca" in exemplo["properties"]
