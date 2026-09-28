namespace ErpFinanceiro.Application.Dashboard;

/// <summary>Um vencimento próximo, para a lista do painel (seção 9 do escopo).</summary>
public sealed record VencimentoProximo(
    Guid ContaPagarId,
    string Fornecedor,
    string Descricao,
    DateOnly Vencimento,
    decimal ValorFinal);

/// <summary>Soma de ValorFinal das contas a pagar de um centro de custo, para o gráfico de pizza do painel.</summary>
public sealed record GastoPorCentroCusto(string CentroCusto, decimal Total, string? Cor);

/// <summary>
/// Indicadores do dashboard financeiro (seção 9 do escopo). "Vencida"/"a
/// vencer"/"hoje"/"próximos 7 dias" são calculados a partir de
/// Vencimento + StatusFinanceiro (não confia só no status gravado, que só
/// muda quando alguém opera a conta — ainda não existe job periódico de
/// recálculo, isso é Fase 2/Hangfire).
/// </summary>
public sealed record IndicadoresPainel(
    decimal TotalEmAberto,
    int QuantidadeEmAberto,
    decimal TotalVencidas,
    int QuantidadeVencidas,
    decimal TotalAPagarHoje,
    decimal TotalAPagarProximos7Dias,
    decimal TotalAPagarNoMes,
    decimal TotalPagoNoMes,
    decimal TotalPagoNoAno,
    decimal TotalDespesasCartaoNoAno,
    decimal TotalPagamentoNaoIdentificado,
    int QuantidadePagamentoNaoIdentificado,
    IReadOnlyList<VencimentoProximo> ProximosVencimentos,
    IReadOnlyList<GastoPorCentroCusto> GastoPorCentroCusto);

public interface IPainelFinanceiro
{
    Task<IndicadoresPainel> ObterAsync();
}
