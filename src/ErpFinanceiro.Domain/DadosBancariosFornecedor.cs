namespace ErpFinanceiro.Domain;

public enum TipoContaBancaria
{
    Corrente,
    Poupanca,
}

/// <summary>
/// Dados bancários de um fornecedor (seção 2 do escopo) — N registros por
/// fornecedor, um marcado como <see cref="Principal"/>. Conta e ChavePix
/// são cifradas em repouso (AES-256, ver CLAUDE.md seção 3) via
/// ValueConverter configurado no AppDbContext, não nesta classe — a
/// entidade de domínio guarda o valor em claro em memória.
/// </summary>
public class DadosBancariosFornecedor : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public string Banco { get; set; } = string.Empty;

    public string Agencia { get; set; } = string.Empty;

    public string Conta { get; set; } = string.Empty;

    public TipoContaBancaria Tipo { get; set; }

    public string? ChavePix { get; set; }

    public bool Principal { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
