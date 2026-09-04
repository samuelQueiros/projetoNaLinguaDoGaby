namespace ErpFinanceiro.Domain;

/// <summary>
/// Conta a pagar — núcleo do sistema (seção 3 do escopo). ValorFinal é
/// calculado em Application (Passo 15), não só no banco, mas persistido
/// aqui para não recalcular a cada leitura/relatório. Nunca excluída
/// fisicamente: <see cref="ExcluidoEm"/> marca a exclusão lógica.
/// </summary>
public class ContaPagar : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public Guid? CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    public Guid? CentroCustoId { get; set; }

    public CentroCusto? CentroCusto { get; set; }

    public DateOnly Vencimento { get; set; }

    public decimal ValorOriginal { get; set; }

    public decimal Desconto { get; set; }

    public decimal Juros { get; set; }

    public decimal Multa { get; set; }

    public decimal ValorFinal { get; set; }

    public Guid? FormaPagamentoId { get; set; }

    public FormaPagamento? FormaPagamento { get; set; }

    public StatusAprovacao StatusAprovacao { get; set; } = StatusAprovacao.Cadastrada;

    public StatusFinanceiro StatusFinanceiro { get; set; } = StatusFinanceiro.EmAberto;

    public Guid CriadoPorId { get; set; }

    public Usuario? CriadoPor { get; set; }

    public string? MotivoCancelamentoRejeicao { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
