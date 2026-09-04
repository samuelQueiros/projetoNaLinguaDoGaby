namespace ErpFinanceiro.Application.ContasAPagar;

public sealed record ContaPagarInput(
    Guid FornecedorId,
    string Descricao,
    Guid? CategoriaId,
    Guid? CentroCustoId,
    DateOnly Vencimento,
    decimal ValorOriginal,
    decimal Desconto,
    decimal Juros,
    decimal Multa,
    Guid? FormaPagamentoId);
