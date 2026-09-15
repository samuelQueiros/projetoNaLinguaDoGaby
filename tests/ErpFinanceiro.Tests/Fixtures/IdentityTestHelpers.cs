using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Fixtures;

/// <summary>
/// UserManager real (não mock) contra o AppDbContext InMemory do teste, e
/// helper pra criar um usuário já com papel atribuído — precisos pra testar
/// as checagens de permissão por papel que os Gerenciadores fazem via
/// UserManager.GetRolesAsync. Usado pelos testes de ContasAPagar/
/// Fornecedores/Cartoes criados a partir da auditoria de RBAC; mesma
/// motivação de AppDbContextFactory (não duplicar a configuração do
/// Identity a cada módulo novo). GerenciadorPagamentosTests, FluxoAprovacaoTests
/// e GerenciadorConfiguracaoIaTests ainda têm cópias próprias (assinatura
/// sem o parâmetro "nome") — candidatos a migrar pra cá depois, não
/// migrados agora pra não alterar 20+ chamadas sem necessidade.
/// </summary>
public static class IdentityTestHelpers
{
    public static UserManager<Usuario> CriarUserManager(AppDbContext db)
    {
        var store = new UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>(db);
        return new UserManager<Usuario>(store, null!, new PasswordHasher<Usuario>(), [], [], null!, null!, null!,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<Usuario>>());
    }

    public static async Task<Usuario> CriarUsuarioComPapelAsync(AppDbContext db, UserManager<Usuario> userManager, string papel, string nome)
    {
        if (!db.Roles.Any(r => r.Name == papel))
        {
            db.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = papel, NormalizedName = papel });
            await db.SaveChangesAsync();
        }

        var usuario = new Usuario { Id = Guid.NewGuid(), UserName = $"{papel}-{Guid.NewGuid():N}@teste.local", Email = $"{papel}-{Guid.NewGuid():N}@teste.local", Nome = nome };
        await userManager.CreateAsync(usuario);
        await userManager.AddToRoleAsync(usuario, papel);
        return usuario;
    }
}
