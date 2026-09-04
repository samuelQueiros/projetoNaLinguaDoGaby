using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Fornecedores;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Fornecedores;

public class GerenciadorFornecedoresTests
{
    private static CriarFornecedorInput InputPadrao(string cnpj = "12.345.678/0001-99") =>
        new("Fornecedor Teste Ltda", "Fornecedor Teste", cnpj, null, null, null, null, null, null, null);

    [Fact]
    public async Task CriarAsync_normaliza_cnpj_removendo_pontuacao()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);

        var fornecedor = await gerenciador.CriarAsync(InputPadrao("12.345.678/0001-99"));

        Assert.Equal("12345678000199", fornecedor.CnpjCpf);
    }

    [Fact]
    public async Task ExcluirAsync_nao_remove_fisicamente_apenas_marca_ExcluidoEm()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);
        var fornecedor = await gerenciador.CriarAsync(InputPadrao());

        var resultado = await gerenciador.ExcluirAsync(fornecedor.Id);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.Fornecedores.FindAsync(fornecedor.Id);
        Assert.NotNull(doBanco);
        Assert.NotNull(doBanco!.ExcluidoEm);
    }

    [Fact]
    public async Task ListarAsync_oculta_excluidos_por_padrao()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);
        var ativo = await gerenciador.CriarAsync(InputPadrao("11.111.111/0001-11"));
        var excluido = await gerenciador.CriarAsync(InputPadrao("22.222.222/0001-22"));
        await gerenciador.ExcluirAsync(excluido.Id);

        var listaPadrao = await gerenciador.ListarAsync();
        var listaCompleta = await gerenciador.ListarAsync(incluirExcluidos: true);

        Assert.Single(listaPadrao);
        Assert.Equal(ativo.Id, listaPadrao[0].Id);
        Assert.Equal(2, listaCompleta.Count);
    }

    [Fact]
    public async Task EditarAsync_com_id_inexistente_retorna_falha()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);

        var resultado = await gerenciador.EditarAsync(Guid.NewGuid(), InputPadrao());

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task AdicionarDadosBancariosAsync_com_dois_principais_mantem_so_o_ultimo()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);
        var fornecedor = await gerenciador.CriarAsync(InputPadrao());

        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, null, Principal: true));
        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco B", "0002", "222-2", TipoContaBancaria.Poupanca, "chave-pix", Principal: true));

        var dados = await db.DadosBancariosFornecedores.Where(d => d.FornecedorId == fornecedor.Id).ToListAsync();

        Assert.Equal(2, dados.Count);
        Assert.Single(dados, d => d.Principal);
        Assert.Equal("Banco B", dados.Single(d => d.Principal).Banco);
    }

    [Fact]
    public async Task AdicionarDadosBancariosAsync_cifra_conta_e_chavePix_em_memoria_tambem()
    {
        // Mesmo com InMemory (que não valida constraints reais do Postgres),
        // o ValueConverter roda e a leitura de volta via EF deve decifrar
        // corretamente para o valor original.
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);
        var fornecedor = await gerenciador.CriarAsync(InputPadrao());

        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "999888777", TipoContaBancaria.Corrente, "chave-pix-teste", Principal: true));

        var dados = await db.DadosBancariosFornecedores.SingleAsync(d => d.FornecedorId == fornecedor.Id);
        Assert.Equal("999888777", dados.Conta);
        Assert.Equal("chave-pix-teste", dados.ChavePix);
    }

    [Fact]
    public async Task RemoverDadosBancariosAsync_remove_o_registro()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorFornecedores(db);
        var fornecedor = await gerenciador.CriarAsync(InputPadrao());
        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, null, Principal: true));
        var dadosId = (await db.DadosBancariosFornecedores.SingleAsync(d => d.FornecedorId == fornecedor.Id)).Id;

        var resultado = await gerenciador.RemoverDadosBancariosAsync(dadosId);

        Assert.True(resultado.Sucesso);
        Assert.Empty(db.DadosBancariosFornecedores.Where(d => d.FornecedorId == fornecedor.Id));
    }
}
