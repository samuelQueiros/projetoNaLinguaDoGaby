using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.Data;

/// <summary>
/// Seed inicial rodado no startup (Passo 4 do plano do MVP): garante que os
/// 4 papéis (<see cref="PerfilUsuario"/>) existam e, se configurado, cria o
/// primeiro usuário Administrador (e, opcionalmente, um segundo usuário de
/// teste com outro perfil, útil em dev para validar autorização por role).
/// Idempotente — seguro rodar em todo startup sem duplicar nada.
///
/// As credenciais vêm de configuração (SeedAdministrador:*/SeedUsuarioTeste:*
/// — ver appsettings.Development.json.example), nunca hardcoded aqui: se não
/// configurado, nenhum usuário é criado (só os papéis).
/// </summary>
public static class SeedInicial
{
    public static async Task AplicarAsync(IServiceProvider servicos, IConfiguration configuracao, ILogger logger)
    {
        var roleManager = servicos.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var perfil in Enum.GetValues<PerfilUsuario>())
        {
            var nome = perfil.ToString();
            if (!await roleManager.RoleExistsAsync(nome))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(nome));
            }
        }

        var userManager = servicos.GetRequiredService<UserManager<Usuario>>();

        await CriarSeConfiguradoAsync(
            userManager, configuracao, logger,
            secao: "SeedAdministrador",
            perfilPadrao: PerfilUsuario.Administrador,
            nomePadrao: "Administrador");

        await CriarSeConfiguradoAsync(
            userManager, configuracao, logger,
            secao: "SeedUsuarioTeste",
            perfilPadrao: PerfilUsuario.Consulta,
            nomePadrao: "Usuário de teste");
    }

    private static async Task CriarSeConfiguradoAsync(
        UserManager<Usuario> userManager,
        IConfiguration configuracao,
        ILogger logger,
        string secao,
        PerfilUsuario perfilPadrao,
        string nomePadrao)
    {
        var email = configuracao[$"{secao}:Email"];
        var senha = configuracao[$"{secao}:SenhaInicial"];
        var nome = configuracao[$"{secao}:Nome"] ?? nomePadrao;
        var perfilTexto = configuracao[$"{secao}:Perfil"];
        var perfil = Enum.TryParse<PerfilUsuario>(perfilTexto, out var perfilConfigurado)
            ? perfilConfigurado
            : perfilPadrao;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            logger.LogInformation(
                "{Secao} não configurado (chaves Email/SenhaInicial ausentes) — nenhum usuário criado por esta seção.",
                secao);
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Nome = nome,
            Ativo = true,
            EmailConfirmed = true,
        };

        var resultado = await userManager.CreateAsync(usuario, senha);
        if (!resultado.Succeeded)
        {
            logger.LogWarning(
                "Falha ao criar usuário a partir de {Secao}: {Erros}",
                secao, string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(usuario, perfil.ToString());
        logger.LogInformation("Usuário criado a partir de {Secao}: {Email} ({Perfil})", secao, email, perfil);
    }
}
