namespace ErpFinanceiro.Domain;

/// <summary>
/// Categoria de despesa (seção 14 do escopo) — cadastro editável usado em
/// ContaPagar, NotaFiscal e CartaoDespesa. Nunca excluída fisicamente:
/// desativada via Ativo = false.
/// </summary>
public class Categoria : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
