using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Usuarios;

/// <summary>
/// Casos de uso básicos de gestão de usuários (Passo 4 do plano do MVP).
/// A implementação (Infrastructure) usa UserManager/RoleManager do ASP.NET
/// Core Identity — a Application não depende diretamente do Identity.
/// </summary>
public interface IGerenciadorUsuarios
{
    Task<ResultadoOperacao> CriarUsuarioAsync(CriarUsuarioInput input);

    /// <summary>
    /// Exclusão lógica: usuário desativado não é removido, apenas perde
    /// acesso (Ativo = false, login bloqueado).
    /// </summary>
    Task<ResultadoOperacao> DesativarUsuarioAsync(Guid usuarioId);

    Task<ResultadoOperacao> TrocarPerfilAsync(Guid usuarioId, PerfilUsuario novoPerfil);
}
