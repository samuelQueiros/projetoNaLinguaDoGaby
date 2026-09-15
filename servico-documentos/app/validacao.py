"""Validação determinística de documentos brasileiros — dígitos
verificadores de CNPJ, CPF e linha digitável de boleto.

Por quê: um LLM (principalmente um modelo fraco/barato) pode "ler" um
CNPJ com um dígito trocado por erro de OCR ou de atenção, e soar
igualmente confiante estando certo ou errado — ele não tem como saber.
Esses algoritmos são determinísticos e baratos (não usam IA nenhuma);
aplicados DEPOIS da extração (regex + LLM), eles corrigem exatamente o
tipo de erro que uma extração por texto livre não consegue perceber
sozinha. Essa é a técnica de maior retorno para "funcionar bem mesmo
com modelo fraco": não pedir pra IA fazer o que um algoritmo já faz
com 100% de certeza.
"""
from __future__ import annotations


def validar_cpf(cpf: str) -> bool:
    """CPF: 11 dígitos, 2 dígitos verificadores (módulo 11)."""
    if len(cpf) != 11 or not cpf.isdigit() or cpf == cpf[0] * 11:
        return False
    for i in (9, 10):
        soma = sum(int(cpf[num]) * ((i + 1) - num) for num in range(i))
        digito = ((soma * 10) % 11) % 10
        if digito != int(cpf[i]):
            return False
    return True


def validar_cnpj(cnpj: str) -> bool:
    """CNPJ: 14 dígitos, 2 dígitos verificadores (módulo 11)."""
    if len(cnpj) != 14 or not cnpj.isdigit() or cnpj == cnpj[0] * 14:
        return False

    pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
    pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]

    def _dv(parcial: str, pesos: list[int]) -> str:
        soma = sum(int(d) * p for d, p in zip(parcial, pesos))
        resto = soma % 11
        return "0" if resto < 2 else str(11 - resto)

    dv1 = _dv(cnpj[:12], pesos1)
    dv2 = _dv(cnpj[:12] + dv1, pesos2)
    return cnpj[12:] == dv1 + dv2


def validar_cnpj_ou_cpf(digitos: str) -> bool:
    """Aceita tanto CNPJ (14) quanto CPF (11) — o campo "cnpj" do schema
    às vezes recebe um CPF (pessoa física emitindo nota/recibo)."""
    if len(digitos) == 14:
        return validar_cnpj(digitos)
    if len(digitos) == 11:
        return validar_cpf(digitos)
    return False


def _modulo10(campo: str) -> str:
    """Dígito verificador módulo 10 (usado nos 3 primeiros campos da
    linha digitável de boleto de cobrança bancária — não arrecadação)."""
    multiplicador = 2
    soma = 0
    for d in reversed(campo):
        produto = int(d) * multiplicador
        soma += produto // 10 + produto % 10
        multiplicador = 1 if multiplicador == 2 else 2
    resto = soma % 10
    return "0" if resto == 0 else str(10 - resto)


def validar_linha_digitavel(linha: str) -> bool | None:
    """Confere os 3 dígitos verificadores (módulo 10) de um boleto de
    cobrança bancária padrão — 47 dígitos: campo 1 (9 dígitos + DV),
    campo 2 (10 dígitos + DV), campo 3 (10 dígitos + DV), seguidos de
    DV geral + fator de vencimento + valor (15 dígitos, não validados
    aqui — dependem do código de barras completo, módulo 11).

    Boletos de arrecadação/concessionária (48 dígitos, módulo 11) não são
    validados aqui — retorna None (não confirma nem reprova) em vez de
    False, pra não gerar um aviso incorreto de "inválido" nesse formato.
    """
    if len(linha) == 48:
        return None  # arrecadação/concessionária — algoritmo diferente, fora de escopo
    if len(linha) != 47:
        return False

    campo1, dv1 = linha[0:9], linha[9]
    campo2, dv2 = linha[10:20], linha[20]
    campo3, dv3 = linha[21:31], linha[31]
    return (
        _modulo10(campo1) == dv1
        and _modulo10(campo2) == dv2
        and _modulo10(campo3) == dv3
    )
