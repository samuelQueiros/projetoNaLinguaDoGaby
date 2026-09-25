namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// Cálculo de ValorFinal de uma conta a pagar (Passo 15 do plano do MVP) —
/// função pura, sem efeitos colaterais, para poder ser testada isoladamente
/// antes de qualquer caso de uso de criar/editar conta usar.
///
/// Fórmula: ValorFinal = ValorOriginal - Desconto + Juros + Multa.
///
/// Decisões de arredondamento/limite (nenhuma estava explícita no escopo,
/// registradas aqui para não ficarem implícitas no código):
/// - Resultado nunca fica negativo (desconto maior que o valor original +
///   juros/multa é clampado em zero) — uma conta a pagar não pode ter
///   valor final negativo.
/// - Arredondamento para 2 casas decimais com <see cref="MidpointRounding.AwayFromZero"/>
///   (arredondamento comercial, ex.: 10,005 -&gt; 10,01), aplicado uma única
///   vez no resultado final, não em cada parcela do cálculo.
/// </summary>
public static class CalculadoraValorFinal
{
    public static decimal Calcular(decimal valorOriginal, decimal desconto, decimal juros, decimal multa)
    {
        var bruto = valorOriginal - desconto + juros + multa;
        var semNegativo = bruto < 0 ? 0 : bruto;
        return Math.Round(semNegativo, 2, MidpointRounding.AwayFromZero);
    }
}
