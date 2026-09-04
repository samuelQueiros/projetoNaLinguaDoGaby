using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.ContasAPagar;

public class GerenciadorContasPagarTests
{
    private sealed class AuditoriaFalsa : IRegistradorAuditoria
    {
        public Task RegistrarAsync(Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo) =>
            Task.CompletedTask;
    }

    private static async Task<(AppDbContext Db, GerenciadorContasPagar Gerenciador, Guid FornecedorId, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Teste", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        await db.SaveChangesAsync();

        var gerenciador = new GerenciadorContasPagar(db, new AuditoriaFalsa());
        return (db, gerenciador, fornecedor.Id, Guid.NewGuid());
    }

    private static ContaPagarInput InputPadrao(Guid fornecedorId, decimal valorOriginal = 100m) =>
        new(fornecedorId, "Aluguel de setembro", null, null, new DateOnly(2026, 9, 30), valorOriginal, 0m, 0m, 0m, null);

    [Fact]
    public async Task CriarAsync_calcula_valor_final_e_define_status_iniciais()
    {
        var (db, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 150m), usuarioId);

        Assert.True(resultado.Operacao.Sucesso);
        Assert.NotNull(resultado.Conta);
        Assert.Equal(150m, resultado.Conta!.ValorFinal);
        Assert.Equal(StatusAprovacao.Cadastrada, resultado.Conta.StatusAprovacao);
        Assert.Equal(StatusFinanceiro.EmAberto, resultado.Conta.StatusFinanceiro);
        Assert.Equal(usuarioId, resultado.Conta.CriadoPorId);
    }

    [Fact]
    public async Task CriarAsync_com_fornecedor_inexistente_retorna_falha()
    {
        var (_, gerenciador, _, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.CriarAsync(InputPadrao(Guid.NewGuid()), usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
        Assert.Null(resultado.Conta);
    }

    [Fact]
    public async Task CriarAsync_com_valor_original_negativo_retorna_falha()
    {
        var (_, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.CriarAsync(InputPadrao(fornecedorId, -10m), usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_desconto_maior_que_valor_original_retorna_falha()
    {
        var (_, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();
        var input = InputPadrao(fornecedorId, 100m) with { Desconto = 200m };

        var resultado = await gerenciador.CriarAsync(input, usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task EditarAsync_recalcula_valor_final()
    {
        var (db, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);

        var novoInput = InputPadrao(fornecedorId, 100m) with { Desconto = 20m };
        var resultado = await gerenciador.EditarAsync(criada.Conta!.Id, novoInput, usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(criada.Conta.Id);
        Assert.Equal(80m, doBanco!.ValorFinal);
    }

    [Fact]
    public async Task ExcluirAsync_nao_remove_fisicamente()
    {
        var (db, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId), usuarioId);

        var resultado = await gerenciador.ExcluirAsync(criada.Conta!.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(criada.Conta.Id);
        Assert.NotNull(doBanco);
        Assert.NotNull(doBanco!.ExcluidoEm);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_status_financeiro_e_fornecedor()
    {
        var (db, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();
        await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);
        var outroFornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Outro", CnpjCpf = "99999999000199" };
        db.Fornecedores.Add(outroFornecedor);
        await db.SaveChangesAsync();
        await gerenciador.CriarAsync(InputPadrao(outroFornecedor.Id, 50m), usuarioId);

        var filtradasPorFornecedor = await gerenciador.ListarAsync(new FiltroContasPagar(FornecedorId: fornecedorId));
        var filtradasPorStatus = await gerenciador.ListarAsync(new FiltroContasPagar(StatusFinanceiro: StatusFinanceiro.EmAberto));

        Assert.Single(filtradasPorFornecedor);
        Assert.Equal(2, filtradasPorStatus.Count);
    }

    [Fact]
    public async Task ListarAsync_oculta_excluidas_por_padrao()
    {
        var (_, gerenciador, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId), usuarioId);
        await gerenciador.ExcluirAsync(criada.Conta!.Id, usuarioId);

        var listaPadrao = await gerenciador.ListarAsync(new FiltroContasPagar());
        var listaCompleta = await gerenciador.ListarAsync(new FiltroContasPagar(IncluirExcluidas: true));

        Assert.Empty(listaPadrao);
        Assert.Single(listaCompleta);
    }
}
