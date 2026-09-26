namespace ErpFinanceiro.Domain;

/// <summary>
/// Fornecedor (seção 2 do escopo). Nunca excluído fisicamente:
/// <see cref="ExcluidoEm"/> marca a exclusão lógica.
/// </summary>
public class Fornecedor : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public string RazaoSocial { get; set; } = string.Empty;

    public string? NomeFantasia { get; set; }

    public string CnpjCpf { get; set; } = string.Empty;

    public string? InscricaoEstadual { get; set; }

    public string? Endereco { get; set; }

    public string? Telefone { get; set; }

    public string? Email { get; set; }

    public string? ContatoResponsavel { get; set; }

    public Guid? FormaPagamentoPadraoId { get; set; }

    public FormaPagamento? FormaPagamentoPadrao { get; set; }

    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public ICollection<DadosBancariosFornecedor> DadosBancarios { get; set; } = new List<DadosBancariosFornecedor>();

    public ICollection<ContratoFornecedor> Contratos { get; set; } = new List<ContratoFornecedor>();
}
