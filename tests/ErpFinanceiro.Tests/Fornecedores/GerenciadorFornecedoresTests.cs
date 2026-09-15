using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.Fornecedores;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Fornecedores;

public class GerenciadorFornecedoresTests
{
    private static async Task<(AppDbContext Db, GerenciadorFornecedores Gerenciador, RegistradorAuditoriaFalso Auditoria, UserManager<Usuario> UserManager, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var usuario = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");
        var auditoria = new RegistradorAuditoriaFalso();
        var gerenciador = new GerenciadorFornecedores(db, auditoria, userManager);
        return (db, gerenciador, auditoria, userManager, usuario.Id);
    }

    private static CriarFornecedorInput InputPadrao(string cnpj = "12.345.678/0001-99") =>
        new("Fornecedor Teste Ltda", "Fornecedor Teste", cnpj, null, null, null, null, null, null, null);

    [Fact]
    public async Task CriarAsync_normaliza_cnpj_removendo_pontuacao()
    {
        var (_, gerenciador, _, _, _) = await PrepararAsync();

        var fornecedor = (await gerenciador.CriarAsync(InputPadrao("12.345.678/0001-99"))).Entidade!;

        Assert.Equal("12345678000199", fornecedor.CnpjCpf);
    }

    // Não há teste automatizado aqui pra "CriarAsync com CNPJ duplicado
    // retorna Falha" — a unicidade é um índice do Postgres
    // (SalvarOuLancarCnpjDuplicadoAsync captura DbUpdateException), e o
    // provider InMemory usado nestes testes não simula constraints de
    // banco real (ver aviso em AppDbContextFactory). Precisa de teste de
    // integração contra Postgres de verdade — CriarAsync agora devolve
    // ResultadoCriacao<Fornecedor> (igual Boleto/NotaFiscal) em vez de
    // lançar exceção pra esse caso, mas isso só é observável nesse cenário
    // específico, que este helper não reproduz.

    [Fact]
    public async Task ExcluirAsync_nao_remove_fisicamente_apenas_marca_ExcluidoEm()
    {
        var (db, gerenciador, _, _, _) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;

        var resultado = await gerenciador.ExcluirAsync(fornecedor.Id);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.Fornecedores.FindAsync(fornecedor.Id);
        Assert.NotNull(doBanco);
        Assert.NotNull(doBanco!.ExcluidoEm);
    }

    [Fact]
    public async Task ListarAsync_oculta_excluidos_por_padrao()
    {
        var (_, gerenciador, _, _, _) = await PrepararAsync();
        var ativo = (await gerenciador.CriarAsync(InputPadrao("11.111.111/0001-11"))).Entidade!;
        var excluido = (await gerenciador.CriarAsync(InputPadrao("22.222.222/0001-22"))).Entidade!;
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
        var (_, gerenciador, _, _, _) = await PrepararAsync();

        var resultado = await gerenciador.EditarAsync(Guid.NewGuid(), InputPadrao());

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task AdicionarDadosBancariosAsync_com_dois_principais_mantem_so_o_ultimo()
    {
        var (db, gerenciador, _, _, usuarioId) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;

        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, null, Principal: true), usuarioId);
        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco B", "0002", "222-2", TipoContaBancaria.Poupanca, "chave-pix", Principal: true), usuarioId);

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
        var (db, gerenciador, _, _, usuarioId) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;

        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "999888777", TipoContaBancaria.Corrente, "chave-pix-teste", Principal: true), usuarioId);

        var dados = await db.DadosBancariosFornecedores.SingleAsync(d => d.FornecedorId == fornecedor.Id);
        Assert.Equal("999888777", dados.Conta);
        Assert.Equal("chave-pix-teste", dados.ChavePix);
    }

    [Fact]
    public async Task RemoverDadosBancariosAsync_remove_o_registro()
    {
        var (db, gerenciador, _, _, usuarioId) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;
        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, null, Principal: true), usuarioId);
        var dadosId = (await db.DadosBancariosFornecedores.SingleAsync(d => d.FornecedorId == fornecedor.Id)).Id;

        var resultado = await gerenciador.RemoverDadosBancariosAsync(dadosId, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Empty(db.DadosBancariosFornecedores.Where(d => d.FornecedorId == fornecedor.Id));
    }

    [Fact]
    public async Task AdicionarDadosBancariosAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, _) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, "chave-pix", Principal: true), consulta.Id);

        Assert.False(resultado.Sucesso);
        Assert.Empty(db.DadosBancariosFornecedores.Where(d => d.FornecedorId == fornecedor.Id));
    }

    [Fact]
    public async Task EditarDadosBancariosAsync_grava_log_de_auditoria_sem_expor_conta_ou_chavePix()
    {
        var (db, gerenciador, auditoria, _, usuarioId) = await PrepararAsync();
        var fornecedor = (await gerenciador.CriarAsync(InputPadrao())).Entidade!;
        await gerenciador.AdicionarDadosBancariosAsync(fornecedor.Id,
            new DadosBancariosInput("Banco A", "0001", "111-1", TipoContaBancaria.Corrente, "chave-pix-original", Principal: true), usuarioId);
        var dadosId = (await db.DadosBancariosFornecedores.SingleAsync(d => d.FornecedorId == fornecedor.Id)).Id;
        auditoria.Chamadas.Clear();

        await gerenciador.EditarDadosBancariosAsync(dadosId,
            new DadosBancariosInput("Banco A", "0001", "999-9", TipoContaBancaria.Corrente, "chave-pix-nova", Principal: true), usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("EditarDadosBancarios", chamada.Acao);
        var valorAnteriorSerializado = System.Text.Json.JsonSerializer.Serialize(chamada.ValorAnterior);
        var valorNovoSerializado = System.Text.Json.JsonSerializer.Serialize(chamada.ValorNovo);
        Assert.DoesNotContain("111-1", valorAnteriorSerializado);
        Assert.DoesNotContain("999-9", valorNovoSerializado);
        Assert.DoesNotContain("chave-pix", valorAnteriorSerializado);
        Assert.DoesNotContain("chave-pix", valorNovoSerializado);
    }
}
