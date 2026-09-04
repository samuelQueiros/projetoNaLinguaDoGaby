using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Categorias;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Categorias;

/// <summary>
/// Testes do caso de uso genérico de cadastro simples (Passo 6 do plano do
/// MVP), usando <see cref="Categoria"/> como entidade de exemplo — a mesma
/// implementação é reutilizada por <see cref="CentroCusto"/>. Cada teste
/// usa um banco EF Core InMemory isolado (Guid único por teste).
///
/// Nota (revisão do test-writer): o índice único parcial de Nome (só entre
/// os ativos, ver AppDbContext) é uma constraint do Postgres que o InMemory
/// não aplica — por isso não há aqui teste de "nome duplicado entre
/// ativos" nem de "reativar colidindo com nome ativo existente": um teste
/// assim passaria mesmo que a regra estivesse quebrada, dando falsa
/// confiança. Fica registrado como lembrete para os Passos 14/15/19
/// (ContaPagar/Pagamento), onde as constraints de integridade importam de
/// verdade: nesse ponto, criar um projeto de teste de integração contra
/// Postgres real (ex.: via Testcontainers) para cobrir especificamente o
/// que o InMemory não consegue validar.
/// </summary>
public class GerenciadorCadastroSimplesTests
{
    [Fact]
    public async Task CriarAsync_deve_persistir_categoria_ativa()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);

        var categoria = await gerenciador.CriarAsync("Aluguel", "Despesas de aluguel");

        Assert.NotEqual(Guid.Empty, categoria.Id);
        Assert.Equal("Aluguel", categoria.Nome);
        Assert.True(categoria.Ativo);
        Assert.Single(db.Categorias);
    }

    [Fact]
    public async Task ListarAsync_com_apenasAtivos_deve_ocultar_inativos()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);

        var ativa = await gerenciador.CriarAsync("Energia", null);
        var inativa = await gerenciador.CriarAsync("Descontinuada", null);
        await gerenciador.InativarAsync(inativa.Id);

        var todas = await gerenciador.ListarAsync(apenasAtivos: false);
        var somenteAtivas = await gerenciador.ListarAsync(apenasAtivos: true);

        Assert.Equal(2, todas.Count);
        Assert.Single(somenteAtivas);
        Assert.Equal(ativa.Id, somenteAtivas[0].Id);
    }

    [Fact]
    public async Task ListarAsync_em_base_vazia_retorna_lista_vazia()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);

        var todas = await gerenciador.ListarAsync();

        Assert.Empty(todas);
    }

    [Fact]
    public async Task InativarAsync_nao_remove_fisicamente_o_registro()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);
        var categoria = await gerenciador.CriarAsync("Manutenção", null);

        var resultado = await gerenciador.InativarAsync(categoria.Id);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.Categorias.FindAsync(categoria.Id);
        Assert.NotNull(doBanco);
        Assert.False(doBanco!.Ativo);
    }

    [Fact]
    public async Task InativarAsync_com_id_vazio_retorna_falha()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);

        var resultado = await gerenciador.InativarAsync(Guid.Empty);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ReativarAsync_volta_o_registro_para_ativo()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);
        var categoria = await gerenciador.CriarAsync("Materiais", null);
        await gerenciador.InativarAsync(categoria.Id);

        var resultado = await gerenciador.ReativarAsync(categoria.Id);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.Categorias.FindAsync(categoria.Id);
        Assert.True(doBanco!.Ativo);
    }

    [Fact]
    public async Task EditarAsync_atualiza_nome_e_descricao()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);
        var categoria = await gerenciador.CriarAsync("Tecnologia", "original");

        var resultado = await gerenciador.EditarAsync(categoria.Id, "Tecnologia da Informação", "atualizada");

        Assert.True(resultado.Sucesso);
        var doBanco = await db.Categorias.FindAsync(categoria.Id);
        Assert.Equal("Tecnologia da Informação", doBanco!.Nome);
        Assert.Equal("atualizada", doBanco.Descricao);
    }

    [Fact]
    public async Task EditarAsync_com_id_inexistente_retorna_falha()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<Categoria>(db);

        var resultado = await gerenciador.EditarAsync(Guid.NewGuid(), "Não existe", null);

        Assert.False(resultado.Sucesso);
        Assert.NotEmpty(resultado.Erros);
    }

    [Fact]
    public async Task CriarAsync_funciona_igualmente_para_CentroCusto()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var gerenciador = new GerenciadorCadastroSimples<CentroCusto>(db);

        var centro = await gerenciador.CriarAsync("Financeiro", null);

        Assert.True(centro.Ativo);
        Assert.Single(db.CentrosDeCusto);
    }
}
