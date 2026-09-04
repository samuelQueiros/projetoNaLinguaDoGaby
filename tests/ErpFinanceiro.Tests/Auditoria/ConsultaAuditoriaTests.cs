using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Auditoria;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Auditoria;

public class ConsultaAuditoriaTests
{
    [Fact]
    public async Task ListarAsync_filtra_por_tipo_entidade_e_ordena_do_mais_recente()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var usuario = new Usuario { Id = Guid.NewGuid(), Nome = "Ana", UserName = "ana@erp.local", Email = "ana@erp.local" };
        db.Users.Add(usuario);
        db.LogsAuditoria.AddRange(
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Criar", TipoEntidade = "Fornecedor", EntidadeId = Guid.NewGuid(), Data = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Editar", TipoEntidade = "Fornecedor", EntidadeId = Guid.NewGuid(), Data = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc) },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Criar", TipoEntidade = "Boleto", EntidadeId = Guid.NewGuid(), Data = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var consulta = new ConsultaAuditoria(db);

        var todos = await consulta.ListarAsync(new FiltroLogAuditoria());
        Assert.Equal(3, todos.Count);
        Assert.Equal("Boleto", todos[0].TipoEntidade); // mais recente primeiro

        var soFornecedor = await consulta.ListarAsync(new FiltroLogAuditoria(TipoEntidade: "Fornecedor"));
        Assert.Equal(2, soFornecedor.Count);
        Assert.All(soFornecedor, r => Assert.Equal("Fornecedor", r.TipoEntidade));
        Assert.Equal("Ana", soFornecedor[0].UsuarioNome);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_periodo()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var usuario = new Usuario { Id = Guid.NewGuid(), Nome = "Ana", UserName = "ana@erp.local", Email = "ana@erp.local" };
        db.Users.Add(usuario);
        db.LogsAuditoria.Add(new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Criar", TipoEntidade = "Fornecedor", EntidadeId = Guid.NewGuid(), Data = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var consulta = new ConsultaAuditoria(db);

        Assert.Single(await consulta.ListarAsync(new FiltroLogAuditoria(DataInicial: new DateTime(2026, 5, 1), DataFinal: new DateTime(2026, 5, 31))));
        Assert.Empty(await consulta.ListarAsync(new FiltroLogAuditoria(DataInicial: new DateTime(2026, 6, 1))));
    }

    [Fact]
    public async Task ListarTiposEntidadeAsync_retorna_valores_distintos_ordenados()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var usuario = new Usuario { Id = Guid.NewGuid(), Nome = "Ana", UserName = "ana@erp.local", Email = "ana@erp.local" };
        db.Users.Add(usuario);
        db.LogsAuditoria.AddRange(
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Criar", TipoEntidade = "NotaFiscal", EntidadeId = Guid.NewGuid(), Data = DateTime.UtcNow },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Criar", TipoEntidade = "Boleto", EntidadeId = Guid.NewGuid(), Data = DateTime.UtcNow },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Acao = "Editar", TipoEntidade = "Boleto", EntidadeId = Guid.NewGuid(), Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var consulta = new ConsultaAuditoria(db);

        var tipos = await consulta.ListarTiposEntidadeAsync();

        Assert.Equal(["Boleto", "NotaFiscal"], tipos);
    }
}
