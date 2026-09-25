namespace ErpFinanceiro.Domain;

/// <summary>
/// Fluxo de aprovação de uma conta a pagar (seção 16 do escopo) — campo
/// independente de <see cref="StatusFinanceiro"/> (decisão 1, seção 6 do
/// CLAUDE.md). Ex.: uma conta pode estar "Aprovada" (aqui) e "A vencer"
/// (StatusFinanceiro) ao mesmo tempo — dois selos, não um enum combinado.
/// </summary>
public enum StatusAprovacao
{
    Cadastrada,
    AguardandoAprovacao,
    Aprovada,
    Rejeitada,
}
