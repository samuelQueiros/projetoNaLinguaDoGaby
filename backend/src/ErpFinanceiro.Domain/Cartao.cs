namespace ErpFinanceiro.Domain;

public enum StatusCartao
{
    Ativo,
    Bloqueado,
    Cancelado,
}

/// <summary>
/// Cartão de crédito/débito usado pela empresa (seção 5 do escopo).
/// </summary>
public class Cartao : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public string InstituicaoFinanceira { get; set; } = string.Empty;

    public string Bandeira { get; set; } = string.Empty;

    public string Apelido { get; set; } = string.Empty;

    public string UltimosQuatroDigitos { get; set; } = string.Empty;

    public decimal Limite { get; set; }

    public int DiaFechamento { get; set; }

    public int DiaVencimento { get; set; }

    public Guid ResponsavelId { get; set; }

    public Usuario? Responsavel { get; set; }

    public StatusCartao Status { get; set; } = StatusCartao.Ativo;

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
