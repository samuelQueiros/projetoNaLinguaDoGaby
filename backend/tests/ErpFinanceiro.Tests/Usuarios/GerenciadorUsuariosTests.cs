using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.Usuarios;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Usuarios;

public class GerenciadorUsuariosTests
{
    private static async Task<(AppDbContext Db, GerenciadorUsuarios Gerenciador, UserManager<Usuario> UserManager, Guid AdminId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        // Normalizer null, igual ao UserManager de IdentityTestHelpers — os
        // dois precisam concordar em como normalizam o nome do papel, senão
        // AddToRoleAsync (via UserManager) não acha o papel que o
        // RoleManager criou com um normalizer diferente (ex.: maiúsculas).
        var roleManager = new RoleManager<IdentityRole<Guid>>(
            new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(db), [], null!,
            null!, new Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleManager<IdentityRole<Guid>>>());
        var admin = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Administrador", "Admin Teste");
        var gerenciador = new GerenciadorUsuarios(db, userManager, roleManager);
        return (db, gerenciador, userManager, admin.Id);
    }

    [Fact]
    public async Task CriarUsuarioAsync_por_Administrador_funciona()
    {
        var (_, gerenciador, userManager, adminId) = await PrepararAsync();

        var resultado = await gerenciador.CriarUsuarioAsync(
            new CriarUsuarioInput("Novo", "novo@teste.local", "Senha@123456", PerfilUsuario.Financeiro), adminId);

        Assert.True(resultado.Sucesso);
        var criado = await userManager.FindByEmailAsync("novo@teste.local");
        Assert.NotNull(criado);
        Assert.Contains("Financeiro", await userManager.GetRolesAsync(criado!));
    }

    [Fact]
    public async Task CriarUsuarioAsync_por_usuario_nao_administrador_e_bloqueado()
    {
        var (db, gerenciador, userManager, _) = await PrepararAsync();
        var financeiro = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");

        var resultado = await gerenciador.CriarUsuarioAsync(
            new CriarUsuarioInput("Novo", "novo2@teste.local", "Senha@123456", PerfilUsuario.Consulta), financeiro.Id);

        Assert.False(resultado.Sucesso);
        Assert.Null(await userManager.FindByEmailAsync("novo2@teste.local"));
    }

    [Fact]
    public async Task DesativarUsuarioAsync_por_usuario_nao_administrador_e_bloqueado()
    {
        var (db, gerenciador, userManager, adminId) = await PrepararAsync();
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.DesativarUsuarioAsync(adminId, consulta.Id);

        Assert.False(resultado.Sucesso);
        var administrador = await userManager.FindByIdAsync(adminId.ToString());
        Assert.True(administrador!.Ativo);
    }

    [Fact]
    public async Task ListarComPapeisAsync_retorna_papeis_de_todos_os_usuarios()
    {
        var (db, gerenciador, userManager, adminId) = await PrepararAsync();
        await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var lista = await gerenciador.ListarComPapeisAsync();

        Assert.Equal(2, lista.Count);
        var admin = lista.Single(l => l.Usuario.Id == adminId);
        Assert.Contains("Administrador", admin.Papeis);
    }
}
