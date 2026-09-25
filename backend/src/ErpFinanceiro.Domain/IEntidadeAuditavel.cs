namespace ErpFinanceiro.Domain;

/// <summary>
/// Contrato para entidades com timestamps de criação/atualização
/// preenchidos automaticamente pelo AppDbContext (SaveChanges) — evita que
/// cada módulo repita essa lógica manualmente. "Quem" fez a alteração fica
/// no LogAuditoria (decisão 2, seção 6 do CLAUDE.md); estes campos só
/// respondem "quando", direto na própria linha, sem join.
/// </summary>
public interface IEntidadeAuditavel
{
    DateTime CriadoEm { get; set; }

    DateTime? AtualizadoEm { get; set; }
}
