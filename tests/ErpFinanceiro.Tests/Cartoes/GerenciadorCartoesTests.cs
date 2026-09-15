using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Cartoes;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Tests.Cartoes;

public class GerenciadorCartoesTests
{
    private static async Task<(AppDbContext Db, GerenciadorCartoes Gerenciador, RegistradorAuditoriaFalso Auditoria, UserManager<Usuario> UserManager, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var usuario = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");
        var auditoria = new RegistradorAuditoriaFalso();
        var gerenciador = new GerenciadorCartoes(db, auditoria, userManager);
        return (db, gerenciador, auditoria, userManager, usuario.Id);
    }

    private static CartaoInput InputPadrao(Guid responsavelId) =>
        new("Banco X", "Visa", "Corp", "1234", 5000m, 5, 15, responsavelId);

    [Fact]
    public async Task CriarAsync_por_usuario_Financeiro_funciona_e_audita()
    {
        var (_, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();

        var cartao = await gerenciador.CriarAsync(InputPadrao(usuarioId), usuarioId);

        Assert.Equal(StatusCartao.Ativo, cartao.Status);
        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("Criar", chamada.Acao);
    }

    [Fact]
    public async Task CriarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, auditoria, userManager, usuarioId) = await PrepararAsync();
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        await Assert.ThrowsAsync<InvalidOperationException>(() => gerenciador.CriarAsync(InputPadrao(usuarioId), consulta.Id));

        Assert.Empty(auditoria.Chamadas);
        Assert.Empty(db.Cartoes);
    }

    [Fact]
    public async Task EditarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, usuarioId) = await PrepararAsync();
        var cartao = await gerenciador.CriarAsync(InputPadrao(usuarioId), usuarioId);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.EditarAsync(cartao.Id, InputPadrao(usuarioId) with { Limite = 999999m }, consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.Cartoes.FindAsync(cartao.Id);
        Assert.Equal(5000m, doBanco!.Limite);
    }
}
