namespace ErpFinanceiro.Domain;

/// <summary>
/// Perfis de acesso do sistema (seção 15 do escopo / seção 5 do CLAUDE.md).
/// Cada valor corresponde a uma role do ASP.NET Core Identity com o mesmo
/// nome — não há coluna própria de "perfil" em Usuario.
/// </summary>
public enum PerfilUsuario
{
    Administrador,
    Financeiro,
    Gestor,
    Consulta,
}
