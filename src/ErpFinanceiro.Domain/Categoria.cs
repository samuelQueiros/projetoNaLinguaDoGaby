namespace ErpFinanceiro.Domain;

/// <summary>
/// Categoria de despesa (seção 14 do escopo) — cadastro editável usado em
/// ContaPagar, NotaFiscal e CartaoDespesa. Nunca excluída fisicamente:
/// desativada via Ativo = false.
/// </summary>
public class Categoria
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;
}
