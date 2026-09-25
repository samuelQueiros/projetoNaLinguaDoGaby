namespace ErpFinanceiro.Domain;

/// <summary>
/// Conta bancária da empresa (de onde o dinheiro sai) — distinta de
/// <see cref="DadosBancariosFornecedor"/> (para onde o dinheiro vai),
/// decisão 3 da seção 6 do CLAUDE.md. Usada em Pagamento e, na Fase 2, na
/// conciliação bancária.
/// </summary>
public class ContaBancariaEmpresa : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public string Banco { get; set; } = string.Empty;

    public string Agencia { get; set; } = string.Empty;

    public string Conta { get; set; } = string.Empty;

    public TipoContaBancaria Tipo { get; set; }

    public string Apelido { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
