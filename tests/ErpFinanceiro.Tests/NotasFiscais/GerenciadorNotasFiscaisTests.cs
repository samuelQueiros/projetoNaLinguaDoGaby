using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.NotasFiscais;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.NotasFiscais;

public class GerenciadorNotasFiscaisTests
{
    private static async Task<(GerenciadorNotasFiscais Gerenciador, RegistradorAuditoriaFalso Auditoria, AppDbContext Db, Guid FornecedorId, Guid UsuarioId)> CriarAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor NF", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        await db.SaveChangesAsync();
        var auditoria = new RegistradorAuditoriaFalso();
        return (new GerenciadorNotasFiscais(db, auditoria), auditoria, db, fornecedor.Id, Guid.NewGuid());
    }

    private static NotaFiscalInput Input(Guid fornecedorId, string numero = "1000", decimal valor = 500m) =>
        new(fornecedorId, null, numero, "1", new DateOnly(2026, 3, 10), valor, new DateOnly(2026, 4, 10), null, null, null);

    [Fact]
    public async Task CriarAsync_valido_grava_e_audita()
    {
        var (gerenciador, auditoria, _, fornecedorId, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(fornecedorId), usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Criar", Assert.Single(auditoria.Chamadas).Acao);
    }

    [Fact]
    public async Task CriarAsync_com_fornecedor_inexistente_falha()
    {
        var (gerenciador, _, _, _, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(Guid.NewGuid()), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_valor_negativo_falha()
    {
        var (gerenciador, _, _, fornecedorId, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(fornecedorId, valor: -1m), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_numero_e_por_fornecedor()
    {
        var (gerenciador, _, db, fornecedorId, usuarioId) = await CriarAsync();
        var outro = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Outro", CnpjCpf = "99999999000188" };
        db.Fornecedores.Add(outro);
        await db.SaveChangesAsync();

        await gerenciador.CriarAsync(Input(fornecedorId, "111"), usuarioId);
        await gerenciador.CriarAsync(Input(fornecedorId, "222"), usuarioId);
        await gerenciador.CriarAsync(Input(outro.Id, "111"), usuarioId);

        Assert.Single(await gerenciador.ListarAsync(new FiltroNotasFiscais(Numero: "111", FornecedorId: fornecedorId)));
        Assert.Equal(2, (await gerenciador.ListarAsync(new FiltroNotasFiscais(FornecedorId: fornecedorId))).Count);
        Assert.Equal(2, (await gerenciador.ListarAsync(new FiltroNotasFiscais(Numero: "111"))).Count);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_cnpj_e_por_periodo()
    {
        var (gerenciador, _, _, fornecedorId, usuarioId) = await CriarAsync();
        await gerenciador.CriarAsync(Input(fornecedorId, "1"), usuarioId);

        Assert.Single(await gerenciador.ListarAsync(new FiltroNotasFiscais(CnpjCpf: "12.345.678/0001-99")));
        Assert.Single(await gerenciador.ListarAsync(new FiltroNotasFiscais(Ano: 2026, Mes: 3)));
        Assert.Empty(await gerenciador.ListarAsync(new FiltroNotasFiscais(Mes: 7)));
    }

    [Fact]
    public async Task ExcluirAsync_remove_e_audita()
    {
        var (gerenciador, auditoria, db, fornecedorId, usuarioId) = await CriarAsync();
        var criada = await gerenciador.CriarAsync(Input(fornecedorId), usuarioId);
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.ExcluirAsync(criada.Entidade!.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Excluir", Assert.Single(auditoria.Chamadas).Acao);
    }
}
