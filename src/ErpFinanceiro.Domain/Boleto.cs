namespace ErpFinanceiro.Domain;

/// <summary>
/// Boleto (seção 8 do escopo). O PDF fica como registro em
/// <see cref="Anexo"/> (EntidadeTipo = Boleto), não como coluna de arquivo
/// aqui — mesma convenção de <see cref="NotaFiscal"/>. Diferente de
/// NotaFiscal, todo boleto pertence a uma conta a pagar (FK obrigatória).
/// </summary>
public class Boleto : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public Guid ContaPagarId { get; set; }

    public ContaPagar? ContaPagar { get; set; }

    public string Numero { get; set; } = string.Empty;

    public string LinhaDigitavel { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    public decimal Valor { get; set; }

    public DateOnly Vencimento { get; set; }

    public DateOnly? DataPagamento { get; set; }

    public string? Banco { get; set; }

    public StatusBoleto Status { get; set; } = StatusBoleto.EmAberto;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
