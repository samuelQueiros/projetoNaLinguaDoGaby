using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ConfiguracoesIa;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErpFinanceiro.Tests.ConfiguracoesIa;

public class GerenciadorConfiguracaoIaTests
{
    private static UserManager<Usuario> CriarUserManager(AppDbContext db)
    {
        var store = new UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>(db);
        return new UserManager<Usuario>(store, null!, new PasswordHasher<Usuario>(), [], [], null!, null!, null!,
            new NullLogger<UserManager<Usuario>>());
    }

    private static async Task<Usuario> CriarUsuarioComPapelAsync(AppDbContext db, UserManager<Usuario> userManager, string papel)
    {
        if (!db.Roles.Any(r => r.Name == papel))
        {
            db.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = papel, NormalizedName = papel });
            await db.SaveChangesAsync();
        }

        var usuario = new Usuario { Id = Guid.NewGuid(), UserName = $"{papel}@teste.local", Email = $"{papel}@teste.local", Nome = papel };
        await userManager.CreateAsync(usuario);
        await userManager.AddToRoleAsync(usuario, papel);
        return usuario;
    }

    private sealed record Cenario(AppDbContext Db, GerenciadorConfiguracaoIa Gerenciador, Usuario Admin, Usuario Financeiro);

    private static async Task<Cenario> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = CriarUserManager(db);
        var auditoria = new RegistradorAuditoriaFalso();

        var admin = await CriarUsuarioComPapelAsync(db, userManager, nameof(PerfilUsuario.Administrador));
        var financeiro = await CriarUsuarioComPapelAsync(db, userManager, nameof(PerfilUsuario.Financeiro));

        // IConfiguration não é usado nos testes abaixo (só entra em
        // TestarConexaoAsync, que faz chamada de rede real — fora do
        // escopo de teste unitário aqui).
        var gerenciador = new GerenciadorConfiguracaoIa(db, auditoria, userManager, new HttpClient(), null!);

        return new Cenario(db, gerenciador, admin, financeiro);
    }

    [Fact]
    public async Task SalvarAsync_sem_papel_administrador_e_recusado()
    {
        var c = await PrepararAsync();
        var input = new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-flash", "chave-teste", true, 90);

        var resultado = await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Documentos, input, c.Financeiro.Id);

        Assert.False(resultado.Sucesso);
        Assert.Empty(await c.Db.ConfiguracoesIa.ToListAsync());
    }

    [Fact]
    public async Task SalvarAsync_como_administrador_cria_a_linha()
    {
        var c = await PrepararAsync();
        var input = new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-flash", "chave-teste", true, 90);

        var resultado = await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Documentos, input, c.Admin.Id);

        Assert.True(resultado.Sucesso);
        var salvo = await c.Db.ConfiguracoesIa.AsNoTracking().SingleAsync(x => x.Finalidade == FinalidadeConfiguracaoIa.Documentos);
        Assert.Equal(ProvedorIa.Gemini, salvo.Provedor);
        Assert.Equal("gemini-2.5-flash", salvo.Modelo);
        Assert.True(salvo.Ativo);
        Assert.Equal("chave-teste", salvo.ApiKey);
    }

    [Fact]
    public async Task SalvarAsync_com_NovaApiKey_nula_mantem_a_chave_existente()
    {
        var c = await PrepararAsync();
        var primeiraChave = new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-flash", "chave-original", true, 90);
        await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Chat, primeiraChave, c.Admin.Id);

        var semTrocarChave = new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-pro", null, true, 120);
        var resultado = await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Chat, semTrocarChave, c.Admin.Id);

        Assert.True(resultado.Sucesso);
        var salvo = await c.Db.ConfiguracoesIa.AsNoTracking().SingleAsync(x => x.Finalidade == FinalidadeConfiguracaoIa.Chat);
        Assert.Equal("chave-original", salvo.ApiKey);
        Assert.Equal("gemini-2.5-pro", salvo.Modelo);
        Assert.Equal(120, salvo.TimeoutSegundos);
    }

    [Fact]
    public async Task SalvarAsync_chave_cifrada_da_roundtrip_correto()
    {
        var c = await PrepararAsync();
        var input = new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-flash", "minha-chave-secreta-123", true, 90);
        await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Documentos, input, c.Admin.Id);

        // A leitura via EF decifra automaticamente (HasConversion) — o
        // valor em memória é sempre texto puro; o que fica cifrado é só o
        // que vai pro banco. Reabrir o contexto simula uma nova requisição.
        var obtido = await c.Gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Documentos);

        Assert.NotNull(obtido);
        Assert.Equal("minha-chave-secreta-123", obtido!.ApiKey);
    }

    [Fact]
    public async Task SalvarAsync_duas_finalidades_ficam_independentes()
    {
        var c = await PrepararAsync();
        await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Documentos,
            new ConfiguracaoIaInput(ProvedorIa.Gemini, "gemini-2.5-flash", "chave-doc", true, 90), c.Admin.Id);
        await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Chat,
            new ConfiguracaoIaInput(ProvedorIa.OpenAi, "gpt-4o-mini", "chave-chat", false, 60), c.Admin.Id);

        var documentos = await c.Gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Documentos);
        var chat = await c.Gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.Equal(ProvedorIa.Gemini, documentos!.Provedor);
        Assert.Equal(ProvedorIa.OpenAi, chat!.Provedor);
        Assert.True(documentos.Ativo);
        Assert.False(chat.Ativo);
    }

    [Fact]
    public async Task SalvarAsync_sem_modelo_e_recusado()
    {
        var c = await PrepararAsync();
        var input = new ConfiguracaoIaInput(ProvedorIa.Gemini, "  ", "chave", true, 90);

        var resultado = await c.Gerenciador.SalvarAsync(FinalidadeConfiguracaoIa.Documentos, input, c.Admin.Id);

        Assert.False(resultado.Sucesso);
    }
}
