namespace ErpFinanceiro.Domain;

/// <summary>
/// Situação financeira de uma conta a pagar (seção 3 do escopo) —
/// independente de <see cref="StatusAprovacao"/> (decisão 1, seção 6 do
/// CLAUDE.md).
/// </summary>
public enum StatusFinanceiro
{
    Agendada,
    EmAberto,
    AVencer,
    Vencida,
    Paga,
    Cancelada,
    PagamentoNaoIdentificado,
    PagamentoRecusadoEstornado,
}
