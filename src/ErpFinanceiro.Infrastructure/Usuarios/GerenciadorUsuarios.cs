using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Usuarios;

public sealed class GerenciadorUsuarios(
    AppDbContext db,
    UserManager<Usuario> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IGerenciadorUsuarios
{
    public async Task<ResultadoOperacao> CriarUsuarioAsync(CriarUsuarioInput input, Guid chamadorId)
    {
        var erroPermissao = await ValidarPermissaoAsync(chamadorId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var usuario = new Usuario
        {
            UserName = input.Email,
            Email = input.Email,
            Nome = input.Nome,
            Ativo = true,
            EmailConfirmed = true,
        };

        var resultado = await userManager.CreateAsync(usuario, input.SenhaInicial);
        if (!resultado.Succeeded)
        {
            return ResultadoOperacao.Falha(resultado.Errors.Select(e => e.Description).ToArray());
        }

        await GarantirPapelAsync(input.Perfil);
        await userManager.AddToRoleAsync(usuario, input.Perfil.ToString());

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> DesativarUsuarioAsync(Guid usuarioId, Guid chamadorId)
    {
        var erroPermissao = await ValidarPermissaoAsync(chamadorId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        // Exclusão lógica: nunca remover o registro, só revogar o acesso.
        usuario.Ativo = false;
        usuario.LockoutEnabled = true;
        usuario.LockoutEnd = DateTimeOffset.MaxValue;

        var resultado = await userManager.UpdateAsync(usuario);
        return resultado.Succeeded
            ? ResultadoOperacao.Ok()
            : ResultadoOperacao.Falha(resultado.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<ResultadoOperacao> TrocarPerfilAsync(Guid usuarioId, PerfilUsuario novoPerfil, Guid chamadorId)
    {
        var erroPermissao = await ValidarPermissaoAsync(chamadorId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        var papeisAtuais = await userManager.GetRolesAsync(usuario);
        if (papeisAtuais.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(usuario, papeisAtuais);
        }

        await GarantirPapelAsync(novoPerfil);
        var resultado = await userManager.AddToRoleAsync(usuario, novoPerfil.ToString());

        return resultado.Succeeded
            ? ResultadoOperacao.Ok()
            : ResultadoOperacao.Falha(resultado.Errors.Select(e => e.Description).ToArray());
    }

    /// <summary>
    /// Uma consulta só (join Users/UserRoles/Roles) em vez de um
    /// GetRolesAsync por usuário — a tela de administração de usuários
    /// fazia N+1 antes (achado da auditoria de qualidade).
    /// </summary>
    public async Task<IReadOnlyList<(Usuario Usuario, IReadOnlyList<string> Papeis)>> ListarComPapeisAsync()
    {
        var linhas = await (
            from u in db.Users.AsNoTracking()
            join ur in db.UserRoles.AsNoTracking() on u.Id equals ur.UserId into papeisDoUsuario
            from ur in papeisDoUsuario.DefaultIfEmpty()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id into papelNome
            from r in papelNome.DefaultIfEmpty()
            select new { Usuario = u, Papel = r.Name })
            .ToListAsync();

        // Agrupa por Id, não pela instância de Usuario — com AsNoTracking()
        // cada linha do join materializa uma instância nova (sem identity
        // map), então agrupar por referência colocaria cada linha no seu
        // próprio grupo.
        return linhas
            .GroupBy(l => l.Usuario.Id)
            .Select(g => (g.First().Usuario, (IReadOnlyList<string>)g.Where(l => l.Papel is not null).Select(l => l.Papel!).ToList()))
            .OrderBy(t => t.Usuario.Nome)
            .ToList();
    }

    /// <summary>
    /// Criar/desativar/trocar perfil de usuário é atribuição exclusiva de
    /// Administrador — mesma restrição já aplicada em Usuarios.razor
    /// ([Authorize(Roles = "Administrador")]), mas antes só na página, não
    /// aqui (achado da auditoria de segurança: defesa em profundidade
    /// ausente — qualquer reuso futuro desta classe fora dessa página
    /// herdaria a falta de proteção).
    /// </summary>
    private async Task<ResultadoOperacao?> ValidarPermissaoAsync(Guid chamadorId)
    {
        var chamador = await userManager.FindByIdAsync(chamadorId.ToString());
        if (chamador is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        if (!await userManager.IsInRoleAsync(chamador, nameof(PerfilUsuario.Administrador)))
        {
            return ResultadoOperacao.Falha("Só usuários com perfil Administrador podem gerenciar outros usuários.");
        }

        return null;
    }

    private async Task GarantirPapelAsync(PerfilUsuario perfil)
    {
        var nomePapel = perfil.ToString();
        if (!await roleManager.RoleExistsAsync(nomePapel))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(nomePapel));
        }
    }
}
