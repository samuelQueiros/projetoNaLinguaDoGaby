"""Configuração via variáveis de ambiente (.env). Ver .env.example."""
from __future__ import annotations

from functools import lru_cache
from typing import Literal

from pydantic_settings import BaseSettings, SettingsConfigDict


class Config(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # LLM — abstraído aqui; o backend .NET só manda provider/modelo/chave
    # como override por requisição (ver /extrair) quando o Administrador
    # configurou isso em /configuracoes-ia — nunca sabe como cada provider
    # é chamado por dentro.
    llm_provider: Literal["none", "anthropic", "openai", "gemini"] = "none"
    llm_api_key: str = ""
    llm_model: str = "claude-sonnet-5"
    llm_timeout_segundos: int = 90
    # Falha de LLM aqui não quebra o upload pro usuário — o pipeline cai
    # pra extração só por regex — mas tentar de novo antes de desistir
    # evita perder a extração por IA num rate limit/erro 5xx passageiro do
    # provider, o caso mais comum na prática (achado da auditoria de
    # qualidade; mesmo padrão de retry+backoff já usado em contrib/erp_client.py).
    llm_max_tentativas: int = 2

    # Upload
    max_arquivo_mb: int = 25

    # OCR
    ocr_habilitado: bool = True
    ocr_idioma: str = "por+eng"
    ocr_limiar_caracteres: int = 40
    # Teto de páginas rasterizadas por OCR — um PDF de poucos MB pode ter
    # milhares de páginas (o limite é MAX_ARQUIVO_MB, não nº de páginas) e
    # rasterizar todas a 200dpi sem limite é um vetor de DoS (achado da
    # auditoria de segurança). Páginas além do teto ficam sem OCR.
    ocr_max_paginas: int = 30

    # Teto de linhas de planilha processadas por upload — mesma lógica do
    # limite de páginas de OCR: um XLSX pequeno pode conter uma quantidade
    # desproporcional de linhas depois de descompactado.
    xlsx_max_linhas: int = 50_000

    # Teto de caracteres do texto integral devolvido em RespostaExtracao.texto
    # (o .NET persiste isso em DocumentoImportado.TextoExtraido). O texto
    # inteiro já é usado internamente pro regex/LLM sem esse corte — é só o
    # que volta pro contrato HTTP e vai pro banco que é limitado, pra não
    # armazenar/trafegar um PDF de centenas de páginas inteiro por engano.
    texto_max_chars: int = 100_000

    # Segredo compartilhado esperado no header X-Internal-Token — sem ele
    # configurado (string vazia, padrão), o endpoint /extrair fica aberto
    # pra qualquer host que alcance a porta, como documentado na auditoria
    # de segurança ("sem autenticação entre o backend .NET e este
    # serviço"). Definir em produção; combina com isolamento de rede
    # (docker network privada), não substitui.
    internal_token: str = ""

    # Log
    log_level: str = "INFO"
    log_texto_bruto: bool = False  # NUNCA true em produção (CPF/CNPJ são sensíveis)

    @property
    def max_arquivo_bytes(self) -> int:
        return self.max_arquivo_mb * 1024 * 1024

    @property
    def usa_llm(self) -> bool:
        return self.llm_provider != "none" and bool(self.llm_api_key)


@lru_cache
def get_config() -> Config:
    return Config()
