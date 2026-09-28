namespace ErpFinanceiro.Domain;

/// <summary>
/// Centro de custo (seção 14 do escopo) — identifica qual setor/projeto
/// gerou uma despesa. Cadastro editável, usado em ContaPagar, NotaFiscal e
/// CartaoDespesa. Nunca excluído fisicamente: desativado via Ativo = false.
/// </summary>
public class CentroCusto : IEntidadeAuditavel, ICadastroSimples, ICadastroComCor
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Cor hex (#rrggbb) usada para identificar o setor no gráfico de pizza do painel.</summary>
    public string? Cor { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
