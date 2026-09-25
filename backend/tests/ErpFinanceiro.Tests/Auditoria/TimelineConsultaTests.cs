using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Auditoria;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Auditoria;

public class TimelineConsultaTests
{
    [Fact]
    public async Task ObterAsync_retorna_eventos_da_conta_em_ordem_cronologica_com_autor()
    {
        // Reproduz o exemplo da seção 17 do escopo: cadastro -> aprovação ->
        // pagamento, cada evento com autor e data (decisão 2 da seção 6 do
        // CLAUDE.md: timeline é LogAuditoria filtrado, não tabela separada).
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var joao = new Usuario { Id = Guid.NewGuid(), Nome = "João" };
        var maria = new Usuario { Id = Guid.NewGuid(), Nome = "Maria" };
        db.Users.AddRange(joao, maria);

        var contaId = Guid.NewGuid();
        var outraContaId = Guid.NewGuid();
        db.LogsAuditoria.AddRange(
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = maria.Id, Acao = "Aprovada", TipoEntidade = nameof(ContaPagar), EntidadeId = contaId, Data = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc) },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = joao.Id, Acao = "Criar", TipoEntidade = nameof(ContaPagar), EntidadeId = contaId, Data = new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc) },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = joao.Id, Acao = "RegistrarPagamento", TipoEntidade = nameof(ContaPagar), EntidadeId = contaId, Data = new DateTime(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc) },
            new LogAuditoria { Id = Guid.NewGuid(), UsuarioId = joao.Id, Acao = "Criar", TipoEntidade = nameof(ContaPagar), EntidadeId = outraContaId, Data = new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var timeline = await new TimelineConsulta(db).ObterAsync(nameof(ContaPagar), contaId);

        Assert.Equal(new[] { "Criar", "Aprovada", "RegistrarPagamento" }, timeline.Select(e => e.Acao));
        Assert.Equal(new[] { "João", "Maria", "João" }, timeline.Select(e => e.NomeUsuario));
    }

    [Fact]
    public async Task ObterAsync_conta_sem_eventos_retorna_lista_vazia()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();

        var timeline = await new TimelineConsulta(db).ObterAsync(nameof(ContaPagar), Guid.NewGuid());

        Assert.Empty(timeline);
    }
}
