namespace ErpFinanceiro.Application.Dashboard;

/// <summary>Um vencimento próximo, para a lista do painel (seção 9 do escopo).</summary>
public sealed record VencimentoProximo(
    Guid ContaPagarId,
    string Fornecedor,
    string Descricao,
    DateOnly Vencimento,
    decimal ValorFinal);

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
    IReadOnlyList<VencimentoProximo> ProximosVencimentos);

public interface IPainelFinanceiro
{
    Task<IndicadoresPainel> ObterAsync();
}
