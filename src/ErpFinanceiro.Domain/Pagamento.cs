namespace ErpFinanceiro.Domain;

/// <summary>
/// Pagamento de uma ContaPagar (seção 3 do escopo) — modelado 1:N em
/// relação à conta para suportar pagamento parcial/estorno sem redesenho
/// futuro. Exatamente um de <see cref="ContaBancariaEmpresaId"/> ou
/// <see cref="CartaoId"/> deve estar preenchido (constraint no banco).
/// </summary>
public class Pagamento : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid ContaPagarId { get; set; }

    public ContaPagar? ContaPagar { get; set; }

    public DateOnly Data { get; set; }

    public decimal ValorPago { get; set; }

    public Guid? FormaPagamentoId { get; set; }

    public FormaPagamento? FormaPagamento { get; set; }

    public Guid? ContaBancariaEmpresaId { get; set; }

    public ContaBancariaEmpresa? ContaBancariaEmpresa { get; set; }

    public Guid? CartaoId { get; set; }

    public Cartao? Cartao { get; set; }

    /// <summary>
    /// Referência a Anexo (comprovante) — FK real só a partir do Passo 23,
    /// quando a entidade Anexo existir. Por ora é só um Guid sem constraint
    /// de FK configurada (Passo 19 do plano).
    /// </summary>
    public Guid? ComprovanteAnexoId { get; set; }

    public StatusPagamento Status { get; set; } = StatusPagamento.Confirmado;

    public string? MotivoEstorno { get; set; }

    public Guid RegistradoPorId { get; set; }

    public Usuario? RegistradoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
