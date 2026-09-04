namespace ErpFinanceiro.Domain;

/// <summary>
/// Nota fiscal (seção 7 do escopo). PDF/XML ficam como registros em
/// <see cref="Anexo"/> (EntidadeTipo = NotaFiscal), não como colunas de
/// arquivo aqui. Pode ou não estar vinculada a uma ContaPagar.
/// </summary>
public class NotaFiscal : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public Guid? ContaPagarId { get; set; }

    public ContaPagar? ContaPagar { get; set; }

    public string Numero { get; set; } = string.Empty;

    public string? Serie { get; set; }

    public DateOnly Emissao { get; set; }

    public decimal Valor { get; set; }

    public DateOnly? Vencimento { get; set; }

    public Guid? CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    public Guid? CentroCustoId { get; set; }

    public CentroCusto? CentroCusto { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
