namespace ErpFinanceiro.Domain;

/// <summary>
/// Log de auditoria único (decisão 2, seção 6 do CLAUDE.md) — a timeline de
/// uma ContaPagar (seção 17 do escopo) é uma consulta filtrada nesta mesma
/// tabela por TipoEntidade+EntidadeId, não uma tabela separada.
/// ValorAnterior/ValorNovo em JSONB.
/// </summary>
public class LogAuditoria
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public string Acao { get; set; } = string.Empty;

    public string TipoEntidade { get; set; } = string.Empty;

    public Guid EntidadeId { get; set; }

    /// <summary>JSON serializado — null quando não aplicável (ex.: criação).</summary>
    public string? ValorAnteriorJson { get; set; }

    /// <summary>JSON serializado — null quando não aplicável (ex.: exclusão).</summary>
    public string? ValorNovoJson { get; set; }

    public string? Ip { get; set; }

    public DateTime Data { get; set; }
}
