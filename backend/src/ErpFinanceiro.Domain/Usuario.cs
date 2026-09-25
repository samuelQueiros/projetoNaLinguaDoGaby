using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Domain;

/// <summary>
/// Usuário do sistema (seção 5 do CLAUDE.md), estendendo o IdentityUser
/// padrão do ASP.NET Core Identity com os campos de negócio do domínio.
/// O perfil (<see cref="PerfilUsuario"/>) é modelado como role do Identity,
/// não como coluna nesta tabela.
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime? UltimoLogin { get; set; }
}
