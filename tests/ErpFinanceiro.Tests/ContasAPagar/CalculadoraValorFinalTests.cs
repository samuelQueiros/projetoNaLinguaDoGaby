using ErpFinanceiro.Application.ContasAPagar;

namespace ErpFinanceiro.Tests.ContasAPagar;

public class CalculadoraValorFinalTests
{
    [Fact]
    public void Calcular_sem_desconto_juros_ou_multa_retorna_valor_original()
    {
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: 100m, desconto: 0m, juros: 0m, multa: 0m);

        Assert.Equal(100m, resultado);
    }

    [Fact]
    public void Calcular_aplica_desconto_juros_e_multa_combinados()
    {
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: 100m, desconto: 10m, juros: 5m, multa: 2m);

        Assert.Equal(97m, resultado); // 100 - 10 + 5 + 2
    }

    [Fact]
    public void Calcular_com_todos_os_valores_zerados_retorna_zero()
    {
        var resultado = CalculadoraValorFinal.Calcular(0m, 0m, 0m, 0m);

        Assert.Equal(0m, resultado);
    }

    [Fact]
    public void Calcular_com_desconto_maior_que_valor_original_nao_fica_negativo()
    {
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: 50m, desconto: 200m, juros: 0m, multa: 0m);

        Assert.Equal(0m, resultado);
    }

    [Fact]
    public void Calcular_com_desconto_maior_mas_juros_multa_compensando_fica_positivo()
    {
        // 50 - 200 + 100 + 60 = 10 (não deveria clampar em zero aqui)
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: 50m, desconto: 200m, juros: 100m, multa: 60m);

        Assert.Equal(10m, resultado);
    }

    [Theory]
    [InlineData(10.005, 10.01)] // meio para cima (AwayFromZero)
    [InlineData(10.004, 10.00)]
    [InlineData(10.015, 10.02)]
    [InlineData(0.001, 0.00)]
    public void Calcular_arredonda_para_duas_casas_com_arredondamento_comercial(double valorOriginal, double esperado)
    {
        var resultado = CalculadoraValorFinal.Calcular((decimal)valorOriginal, 0m, 0m, 0m);

        Assert.Equal((decimal)esperado, resultado);
    }

    [Fact]
    public void Calcular_com_valor_muito_grande_nao_estoura()
    {
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: 999_999_999.99m, desconto: 0m, juros: 0m, multa: 0m);

        Assert.Equal(999_999_999.99m, resultado);
    }

    [Fact]
    public void Calcular_com_valor_original_negativo_por_erro_de_entrada_nao_fica_negativo()
    {
        // Regra de negócio não deveria permitir valor original negativo (validação
        // é responsabilidade do caso de uso, Passo 16) — mas a calculadora em si
        // não deve propagar um resultado negativo mesmo que receba entrada inválida.
        var resultado = CalculadoraValorFinal.Calcular(valorOriginal: -10m, desconto: 0m, juros: 0m, multa: 0m);

        Assert.Equal(0m, resultado);
    }
}
