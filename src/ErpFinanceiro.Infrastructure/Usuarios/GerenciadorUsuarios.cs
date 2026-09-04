using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Infrastructure.Usuarios;

public sealed class GerenciadorUsuarios(
    UserManager<Usuario> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IGerenciadorUsuarios
{
    public async Task<ResultadoOperacao> CriarUsuarioAsync(CriarUsuarioInput input)
    {
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

    public async Task<ResultadoOperacao> DesativarUsuarioAsync(Guid usuarioId)
    {
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

    public async Task<ResultadoOperacao> TrocarPerfilAsync(Guid usuarioId, PerfilUsuario novoPerfil)
    {
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

    private async Task GarantirPapelAsync(PerfilUsuario perfil)
    {
        var nomePapel = perfil.ToString();
        if (!await roleManager.RoleExistsAsync(nomePapel))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(nomePapel));
        }
    }
}
