namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>Exatamente um de ContaBancariaEmpresaId/CartaoId deve ser informado.</summary>
public sealed record RegistrarPagamentoInput(
    DateOnly Data,
    decimal ValorPago,
    Guid? FormaPagamentoId,
    Guid? ContaBancariaEmpresaId,
    Guid? CartaoId);
