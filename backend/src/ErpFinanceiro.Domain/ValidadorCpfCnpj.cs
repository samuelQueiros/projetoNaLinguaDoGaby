namespace ErpFinanceiro.Domain;

/// <summary>
/// Valida CPF (11 dígitos) e CNPJ (14 dígitos) pelos dígitos verificadores
/// (módulo 11) — mesmo algoritmo já usado em servico-documentos/app/validacao.py
/// para conferir números extraídos de documentos por IA. Aqui garante que o
/// usuário não cadastre um fornecedor com CPF/CNPJ com dígito trocado por
/// erro de digitação. Espera a string já normalizada (só dígitos).
/// </summary>
public static class ValidadorCpfCnpj
{
    public static bool EhValido(string digitos) => digitos.Length switch
    {
        11 => ValidarCpf(digitos),
        14 => ValidarCnpj(digitos),
        _ => false,
    };

    private static bool ValidarCpf(string cpf)
    {
        if (!EhSequenciaDeDigitos(cpf) || TodosDigitosIguais(cpf))
        {
            return false;
        }

        for (var i = 9; i <= 10; i++)
        {
            var soma = 0;
            for (var num = 0; num < i; num++)
            {
                soma += (cpf[num] - '0') * ((i + 1) - num);
            }

            var digito = soma * 10 % 11 % 10;
            if (digito != cpf[i] - '0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (!EhSequenciaDeDigitos(cnpj) || TodosDigitosIguais(cnpj))
        {
            return false;
        }

        ReadOnlySpan<int> pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        ReadOnlySpan<int> pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var dv1 = DigitoVerificador(cnpj[..12], pesos1);
        var dv2 = DigitoVerificador(cnpj[..12] + dv1, pesos2);

        return cnpj[12..] == $"{dv1}{dv2}";
    }

    private static int DigitoVerificador(string parcial, ReadOnlySpan<int> pesos)
    {
        var soma = 0;
        for (var i = 0; i < parcial.Length; i++)
        {
            soma += (parcial[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static bool EhSequenciaDeDigitos(string valor)
    {
        foreach (var c in valor)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TodosDigitosIguais(string valor)
    {
        foreach (var c in valor)
        {
            if (c != valor[0])
            {
                return false;
            }
        }

        return true;
    }
}
