using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Cartoes;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Tests.Cartoes;

public class GerenciadorContasBancariasEmpresaTests
{
    private static async Task<(AppDbContext Db, GerenciadorContasBancariasEmpresa Gerenciador, RegistradorAuditoriaFalso Auditoria, UserManager<Usuario> UserManager, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var usuario = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");
        var auditoria = new RegistradorAuditoriaFalso();
        var gerenciador = new GerenciadorContasBancariasEmpresa(db, auditoria, userManager);
        return (db, gerenciador, auditoria, userManager, usuario.Id);
    }

    private static ContaBancariaEmpresaInput InputPadrao() =>
        new("Banco X", "0001", "12345-6", TipoContaBancaria.Corrente, "Principal");

    [Fact]
    public async Task CriarAsync_por_usuario_Financeiro_funciona_e_audita()
    {
        var (_, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();

        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);

        Assert.True(conta.Ativo);
        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("Criar", chamada.Acao);
        Assert.Equal(usuarioId, chamada.UsuarioId);
    }

    [Fact]
    public async Task CriarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, auditoria, userManager, _) = await PrepararAsync();
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        await Assert.ThrowsAsync<InvalidOperationException>(() => gerenciador.CriarAsync(InputPadrao(), consulta.Id));

        Assert.Empty(auditoria.Chamadas);
        Assert.Empty(db.ContasBancariasEmpresa);
    }

    [Fact]
    public async Task EditarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, usuarioId) = await PrepararAsync();
        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.EditarAsync(conta.Id, InputPadrao() with { Apelido = "Alterado" }, consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasBancariasEmpresa.FindAsync(conta.Id);
        Assert.Equal("Principal", doBanco!.Apelido);
    }

    [Fact]
    public async Task InativarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, usuarioId) = await PrepararAsync();
        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.InativarAsync(conta.Id, consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasBancariasEmpresa.FindAsync(conta.Id);
        Assert.True(doBanco!.Ativo);
    }
}
