from __future__ import annotations

from app.validacao import (
    validar_cnpj,
    validar_cnpj_ou_cpf,
    validar_cpf,
    validar_linha_digitavel,
)


def test_cnpj_valido():
    assert validar_cnpj("11222333000181") is True


def test_cnpj_com_digito_verificador_errado():
    assert validar_cnpj("11222333000182") is False


def test_cnpj_todos_digitos_iguais_e_invalido():
    assert validar_cnpj("11111111111111") is False


def test_cnpj_tamanho_errado_e_invalido():
    assert validar_cnpj("123") is False


def test_cpf_valido():
    assert validar_cpf("11144477735") is True


def test_cpf_com_digito_verificador_errado():
    assert validar_cpf("11144477736") is False


def test_validar_cnpj_ou_cpf_aceita_os_dois_tamanhos():
    assert validar_cnpj_ou_cpf("11222333000181") is True
    assert validar_cnpj_ou_cpf("11144477735") is True
    assert validar_cnpj_ou_cpf("123") is False


def _linha_valida(c1: str = "341917900", c2: str = "0104351004", c3: str = "7910201510") -> str:
    """Monta uma linha digitável de 47 dígitos coerente, calculando os DVs
    pelo próprio módulo 10 — não depende de um boleto real: campo 1 tem 9
    dígitos + DV, campos 2 e 3 têm 10 dígitos + DV cada (padrão Febraban),
    e os últimos 15 dígitos são DV geral + fator de vencimento + valor."""
    from app.validacao import _modulo10
    return c1 + _modulo10(c1) + c2 + _modulo10(c2) + c3 + _modulo10(c3) + "008912300002480"


def test_linha_digitavel_47_digitos_valida():
    linha = _linha_valida()
    assert len(linha) == 47
    assert validar_linha_digitavel(linha) is True


def test_linha_digitavel_com_dv_errado_e_invalida():
    linha = list(_linha_valida())
    linha[9] = str((int(linha[9]) + 1) % 10)  # estraga só o DV do campo 1
    assert validar_linha_digitavel("".join(linha)) is False


def test_linha_digitavel_48_digitos_nao_e_validada():
    assert validar_linha_digitavel("0" * 48) is None


def test_linha_digitavel_tamanho_invalido():
    assert validar_linha_digitavel("123") is False
