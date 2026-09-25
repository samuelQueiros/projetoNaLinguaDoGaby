namespace ErpFinanceiro.Domain;

/// <summary>
/// Forma de pagamento (seção 4 do escopo) — cadastro editável (PIX,
/// Transferência, TED, DOC, Boleto, Cartão crédito/débito, Débito
/// automático, Dinheiro, Outros), semeado no Passo 8b.
/// </summary>
public class FormaPagamento : IEntidadeAuditavel, ICadastroSimples
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
