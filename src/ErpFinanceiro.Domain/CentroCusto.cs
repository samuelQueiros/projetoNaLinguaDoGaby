namespace ErpFinanceiro.Domain;

/// <summary>
/// Centro de custo (seção 14 do escopo) — identifica qual setor/projeto
/// gerou uma despesa. Cadastro editável, usado em ContaPagar, NotaFiscal e
/// CartaoDespesa. Nunca excluído fisicamente: desativado via Ativo = false.
/// </summary>
public class CentroCusto
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;
}
