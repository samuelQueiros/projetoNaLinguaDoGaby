using System.Text;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.Fornecedores;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErpFinanceiro.Tests.Fornecedores;

public class ContratosFornecedorTests
{
    private static async Task<(AppDbContext Db, GerenciadorFornecedores Gerenciador, RegistradorAuditoriaFalso Auditoria, ArmazenamentoAnexosFalso Storage, Guid FornecedorId, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var usuario = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");
        var auditoria = new RegistradorAuditoriaFalso();
        var storage = new ArmazenamentoAnexosFalso();
        var gerenciador = new GerenciadorFornecedores(db, auditoria, userManager, storage, NullLogger<GerenciadorFornecedores>.Instance);

        var fornecedor = (await gerenciador.CriarAsync(new("Fornecedor Teste Ltda", "Fornecedor Teste", "12345678000199",
            null, null, null, null, null, null, null))).Entidade!;

        return (db, gerenciador, auditoria, storage, fornecedor.Id, usuario.Id);
    }

    private static NovoContratoInput Novo(string nome = "Contrato de Fornecimento", string arquivo = "contrato.pdf",
        DateOnly? inicio = null, DateOnly? fim = null) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes("conteudo")), arquivo, "application/pdf", nome,
            inicio ?? new DateOnly(2026, 1, 1), fim ?? new DateOnly(2026, 12, 31));

    [Fact]
    public async Task AdicionarContratoAsync_grava_metadados_arquivo_e_audita()
    {
        var (_, gerenciador, auditoria, storage, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.AdicionarContratoAsync(fornecedorId, Novo("Contrato Anual", "anual.pdf"), usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Contrato Anual", resultado.Entidade!.Nome);
        Assert.Equal("anual.pdf", resultado.Entidade.NomeArquivo);
        Assert.Single(storage.Arquivos);
        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("AnexarContratoFornecedor", chamada.Acao);
        Assert.Equal(fornecedorId, chamada.EntidadeId);
    }

    [Fact]
    public async Task AdicionarContratoAsync_organiza_pasta_por_fornecedor_e_ano_de_vigencia_sem_usar_nome_original_no_arquivo()
    {
        var (_, gerenciador, _, storage, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.AdicionarContratoAsync(fornecedorId,
            Novo(arquivo: "contrato-sigiloso.pdf", inicio: new DateOnly(2027, 3, 1), fim: new DateOnly(2027, 12, 31)), usuarioId);

        Assert.True(resultado.Sucesso);
        var caminho = resultado.Entidade!.CaminhoArmazenamento;
        Assert.StartsWith("onrtdpj/fornecedores/2027/fornecedor-teste-", caminho);
        Assert.DoesNotContain("contrato-sigiloso", caminho);
        Assert.Single(storage.Arquivos);
    }

    [Fact]
    public async Task AdicionarContratoAsync_com_vigencia_fim_anterior_ao_inicio_retorna_falha()
    {
        var (_, gerenciador, _, storage, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.AdicionarContratoAsync(fornecedorId,
            Novo(inicio: new DateOnly(2026, 6, 1), fim: new DateOnly(2026, 1, 1)), usuarioId);

        Assert.False(resultado.Sucesso);
        Assert.Empty(storage.Arquivos);
    }

    [Fact]
    public async Task AdicionarContratoAsync_com_fornecedor_inexistente_retorna_falha()
    {
        var (_, gerenciador, _, _, _, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.AdicionarContratoAsync(Guid.NewGuid(), Novo(), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ListarContratosAsync_retorna_so_os_contratos_do_fornecedor()
    {
        var (db, gerenciador, _, _, fornecedorId, usuarioId) = await PrepararAsync();
        var outroFornecedor = (await gerenciador.CriarAsync(new("Outro Fornecedor", null, "98765432000188",
            null, null, null, null, null, null, null))).Entidade!;
        await gerenciador.AdicionarContratoAsync(fornecedorId, Novo("Contrato A"), usuarioId);
        await gerenciador.AdicionarContratoAsync(fornecedorId, Novo("Contrato B"), usuarioId);
        await gerenciador.AdicionarContratoAsync(outroFornecedor.Id, Novo("Contrato C"), usuarioId);

        var lista = await gerenciador.ListarContratosAsync(fornecedorId);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task BaixarContratoAsync_recupera_conteudo_original()
    {
        var (_, gerenciador, _, _, fornecedorId, usuarioId) = await PrepararAsync();
        var contrato = (await gerenciador.AdicionarContratoAsync(fornecedorId, Novo(), usuarioId)).Entidade!;

        var download = await gerenciador.BaixarContratoAsync(contrato.Id);

        Assert.NotNull(download);
        using var reader = new StreamReader(download!.Conteudo);
        Assert.Equal("conteudo", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task RemoverContratoAsync_remove_linha_arquivo_e_audita()
    {
        var (db, gerenciador, auditoria, storage, fornecedorId, usuarioId) = await PrepararAsync();
        var contrato = (await gerenciador.AdicionarContratoAsync(fornecedorId, Novo(), usuarioId)).Entidade!;
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.RemoverContratoAsync(contrato.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Empty(storage.Arquivos);
        Assert.Equal(0, await db.ContratosFornecedor.CountAsync());
        Assert.Equal("ExcluirContratoFornecedor", Assert.Single(auditoria.Chamadas).Acao);
    }

    [Fact]
    public async Task RemoverContratoAsync_inexistente_retorna_falha()
    {
        var (_, gerenciador, _, _, _, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.RemoverContratoAsync(Guid.NewGuid(), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RemoverContratoAsync_quando_delete_fisico_falha_mantem_o_registro_no_banco()
    {
        // Mesma garantia de GerenciadorAnexos: o arquivo físico é apagado
        // ANTES do registro no banco — se o delete físico falhar, a operação
        // inteira falha e nada muda, em vez de deixar um arquivo órfão em
        // disco sem que ninguém perceba.
        var (db, gerenciador, auditoria, storage, fornecedorId, usuarioId) = await PrepararAsync();
        var contrato = (await gerenciador.AdicionarContratoAsync(fornecedorId, Novo(), usuarioId)).Entidade!;
        auditoria.Chamadas.Clear();
        storage.FalharAoExcluir = true;

        var resultado = await gerenciador.RemoverContratoAsync(contrato.Id, usuarioId);

        Assert.False(resultado.Sucesso);
        Assert.Equal(1, await db.ContratosFornecedor.CountAsync());
        Assert.Empty(auditoria.Chamadas);
    }
}
