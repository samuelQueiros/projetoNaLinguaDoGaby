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
        new("001", "0001", "123456", TipoContaBancaria.Corrente, "Principal");

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

    [Theory]
    [InlineData("Banco X", "0001", "123456", "Banco deve conter somente números.")]
    [InlineData("001", "0001-X", "123456", "Agência deve conter somente números.")]
    [InlineData("001", "0001", "12345-6", "Conta deve conter somente números.")]
    public async Task CriarAsync_com_campo_bancario_nao_numerico_e_bloqueado(
        string banco, string agencia, string conta, string mensagem)
    {
        var (db, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();
        var input = InputPadrao() with { Banco = banco, Agencia = agencia, Conta = conta };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => gerenciador.CriarAsync(input, usuarioId));

        Assert.Equal(mensagem, erro.Message);
        Assert.Empty(db.ContasBancariasEmpresa);
        Assert.Empty(auditoria.Chamadas);
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
    public async Task EditarAsync_com_conta_nao_numerica_retorna_falha()
    {
        var (db, gerenciador, _, _, usuarioId) = await PrepararAsync();
        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);

        var resultado = await gerenciador.EditarAsync(
            conta.Id, InputPadrao() with { Conta = "12345-6" }, usuarioId);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Conta deve conter somente números.", Assert.Single(resultado.Erros));
        Assert.Equal("123456", (await db.ContasBancariasEmpresa.FindAsync(conta.Id))!.Conta);
    }

    [Fact]
    public async Task ExcluirAsync_conta_sem_pagamentos_remove_e_audita()
    {
        var (db, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();
        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.ExcluirAsync(conta.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Null(await db.ContasBancariasEmpresa.FindAsync(conta.Id));
        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("Excluir", chamada.Acao);
        Assert.Equal(conta.Id, chamada.EntidadeId);
    }

    [Fact]
    public async Task ExcluirAsync_conta_com_pagamento_retorna_falha()
    {
        var (db, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();
        var conta = await gerenciador.CriarAsync(InputPadrao(), usuarioId);
        db.Pagamentos.Add(new Pagamento
        {
            Id = Guid.NewGuid(),
            ContaPagarId = Guid.NewGuid(),
            Data = new DateOnly(2026, 9, 29),
            ValorPago = 100m,
            ContaBancariaEmpresaId = conta.Id,
            RegistradoPorId = usuarioId,
        });
        await db.SaveChangesAsync();
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.ExcluirAsync(conta.Id, usuarioId);

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "Esta conta bancária possui pagamentos vinculados e não pode ser excluída.",
            Assert.Single(resultado.Erros));
        Assert.NotNull(await db.ContasBancariasEmpresa.FindAsync(conta.Id));
        Assert.Empty(auditoria.Chamadas);
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
