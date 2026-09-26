using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.ContasAPagar;

public class GerenciadorContasPagarTests
{
    private sealed class RelogioFixo(DateOnly hoje) : IRelogio
    {
        public DateOnly Hoje() => hoje;
    }

    private static async Task<(AppDbContext Db, GerenciadorContasPagar Gerenciador, RegistradorAuditoriaFalso Auditoria, Guid FornecedorId, Guid UsuarioId)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Teste", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        await db.SaveChangesAsync();

        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var usuario = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");

        var auditoria = new RegistradorAuditoriaFalso();
        var gerenciador = new GerenciadorContasPagar(db, auditoria, userManager, new RelogioFixo(new DateOnly(2026, 9, 1)));
        return (db, gerenciador, auditoria, fornecedor.Id, usuario.Id);
    }

    private static ContaPagarInput InputPadrao(Guid fornecedorId, decimal valorOriginal = 100m) =>
        new(fornecedorId, "Aluguel de setembro", null, null, new DateOnly(2026, 9, 30), valorOriginal, 0m, 0m, 0m, null);

    [Fact]
    public async Task CriarAsync_calcula_valor_final_e_define_status_iniciais()
    {
        var (db, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();

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
        var (_, gerenciador, _, _, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.CriarAsync(InputPadrao(Guid.NewGuid()), usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
        Assert.Null(resultado.Conta);
    }

    [Fact]
    public async Task CriarAsync_com_valor_original_negativo_retorna_falha()
    {
        var (_, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();

        var resultado = await gerenciador.CriarAsync(InputPadrao(fornecedorId, -10m), usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_desconto_maior_que_valor_original_retorna_falha()
    {
        var (_, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var input = InputPadrao(fornecedorId, 100m) with { Desconto = 200m };

        var resultado = await gerenciador.CriarAsync(input, usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_vencimento_anterior_a_hoje_retorna_falha()
    {
        var (_, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var input = InputPadrao(fornecedorId) with { Vencimento = new DateOnly(2026, 8, 31) };

        var resultado = await gerenciador.CriarAsync(input, usuarioId);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task EditarAsync_permite_vencimento_anterior_a_hoje_em_conta_ja_existente()
    {
        // A regra é "não deixar nascer já vencida" — não impede ajustar uma
        // conta que legitimamente já está vencida (ex.: só corrigir o valor).
        var (db, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);

        var resultado = await gerenciador.EditarAsync(criada.Conta!.Id,
            InputPadrao(fornecedorId, 100m) with { Vencimento = new DateOnly(2026, 8, 31) }, usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(criada.Conta.Id);
        Assert.Equal(new DateOnly(2026, 8, 31), doBanco!.Vencimento);
    }

    [Fact]
    public async Task EditarAsync_recalcula_valor_final()
    {
        var (db, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();
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
        var (db, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();
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
        var (db, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();
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
    public async Task CriarAsync_grava_log_de_auditoria()
    {
        var (_, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();

        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal(usuarioId, chamada.UsuarioId);
        Assert.Equal("Criar", chamada.Acao);
        Assert.Equal(nameof(ContaPagar), chamada.TipoEntidade);
        Assert.Equal(criada.Conta!.Id, chamada.EntidadeId);
    }

    [Fact]
    public async Task EditarAsync_grava_log_de_auditoria()
    {
        var (_, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);
        auditoria.Chamadas.Clear(); // ignora a chamada do Criar, só interessa o Editar

        await gerenciador.EditarAsync(criada.Conta!.Id, InputPadrao(fornecedorId, 100m) with { Desconto = 10m }, usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("Editar", chamada.Acao);
        Assert.Equal(criada.Conta.Id, chamada.EntidadeId);
    }

    [Fact]
    public async Task ExcluirAsync_grava_log_de_auditoria()
    {
        var (_, gerenciador, auditoria, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId), usuarioId);
        auditoria.Chamadas.Clear();

        await gerenciador.ExcluirAsync(criada.Conta!.Id, usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("Excluir", chamada.Acao);
        Assert.Equal(criada.Conta.Id, chamada.EntidadeId);
    }

    [Fact]
    public async Task ListarPaginadoAsync_pagina_no_banco_e_devolve_total_real()
    {
        var (_, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        for (var i = 0; i < 5; i++)
        {
            await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m + i) with { Descricao = $"Conta {i}" }, usuarioId);
        }

        var pagina1 = await gerenciador.ListarPaginadoAsync(new FiltroContasPagar(FornecedorId: fornecedorId, TamanhoPagina: 2, Pagina: 1));
        var pagina2 = await gerenciador.ListarPaginadoAsync(new FiltroContasPagar(FornecedorId: fornecedorId, TamanhoPagina: 2, Pagina: 2));
        var pagina3 = await gerenciador.ListarPaginadoAsync(new FiltroContasPagar(FornecedorId: fornecedorId, TamanhoPagina: 2, Pagina: 3));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(3, pagina1.TotalPaginas);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.Single(pagina3.Itens);
        Assert.Empty(pagina1.Itens.Select(c => c.Id).Intersect(pagina2.Itens.Select(c => c.Id)));
    }

    [Fact]
    public async Task ListarPaginadoAsync_aplica_os_mesmos_filtros_de_ListarAsync()
    {
        var (db, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);
        var outroFornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Outro", CnpjCpf = "99999999000199" };
        db.Fornecedores.Add(outroFornecedor);
        await db.SaveChangesAsync();
        await gerenciador.CriarAsync(InputPadrao(outroFornecedor.Id, 50m), usuarioId);

        var resultado = await gerenciador.ListarPaginadoAsync(new FiltroContasPagar(FornecedorId: fornecedorId));

        Assert.Equal(1, resultado.Total);
        Assert.Single(resultado.Itens);
    }

    [Fact]
    public async Task ListarAsync_oculta_excluidas_por_padrao()
    {
        var (_, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId), usuarioId);
        await gerenciador.ExcluirAsync(criada.Conta!.Id, usuarioId);

        var listaPadrao = await gerenciador.ListarAsync(new FiltroContasPagar());
        var listaCompleta = await gerenciador.ListarAsync(new FiltroContasPagar(IncluirExcluidas: true));

        Assert.Empty(listaPadrao);
        Assert.Single(listaCompleta);
    }

    [Fact]
    public async Task CriarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, fornecedorId, _) = await PrepararAsync();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.CriarAsync(InputPadrao(fornecedorId), consulta.Id);

        Assert.False(resultado.Operacao.Sucesso);
        Assert.Empty(db.ContasPagar);
    }

    [Fact]
    public async Task EditarAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId, 100m), usuarioId);
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.EditarAsync(criada.Conta!.Id, InputPadrao(fornecedorId, 999m), consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(criada.Conta.Id);
        Assert.Equal(100m, doBanco!.ValorFinal);
    }

    [Fact]
    public async Task ExcluirAsync_por_usuario_Consulta_e_bloqueado()
    {
        var (db, gerenciador, _, fornecedorId, usuarioId) = await PrepararAsync();
        var criada = await gerenciador.CriarAsync(InputPadrao(fornecedorId), usuarioId);
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var consulta = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.ExcluirAsync(criada.Conta!.Id, consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(criada.Conta.Id);
        Assert.Null(doBanco!.ExcluidoEm);
    }
}
