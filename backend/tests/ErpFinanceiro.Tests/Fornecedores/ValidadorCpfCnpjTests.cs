using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Tests.Fornecedores;

public class ValidadorCpfCnpjTests
{
    [Theory]
    [InlineData("52998224725")] // CPF válido
    [InlineData("11122233396")] // CPF válido
    [InlineData("11222333000181")] // CNPJ válido
    [InlineData("12345678000195")] // CNPJ válido
    public void EhValido_aceita_cpf_e_cnpj_com_digitos_verificadores_corretos(string digitos)
    {
        Assert.True(ValidadorCpfCnpj.EhValido(digitos));
    }

    [Theory]
    [InlineData("52998224726")] // CPF com último dígito verificador errado
    [InlineData("11122233344")] // CPF com ambos dígitos verificadores errados
    [InlineData("12345678000199")] // CNPJ com dígitos verificadores errados
    [InlineData("11111111111")] // CPF com todos os dígitos iguais
    [InlineData("11111111111111")] // CNPJ com todos os dígitos iguais
    [InlineData("123456789")] // tamanho menor que CPF
    [InlineData("123456789012345")] // tamanho maior que CNPJ
    [InlineData("")]
    [InlineData("1234567890a")] // contém caractere não numérico
    public void EhValido_rejeita_documentos_invalidos(string digitos)
    {
        Assert.False(ValidadorCpfCnpj.EhValido(digitos));
    }
}
