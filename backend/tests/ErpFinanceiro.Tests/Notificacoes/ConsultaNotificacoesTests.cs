using ErpFinanceiro.Application;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Notificacoes;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Notificacoes;

public class ConsultaNotificacoesTests
{
    private sealed class RelogioFixo(DateOnly hoje) : IRelogio
    {
        public DateOnly Hoje() => hoje;
    }

    [Fact]
    public async Task ListarAsync_retorna_contratos_e_contas_em_aberto_que_vencem_amanha()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var hoje = new DateOnly(2026, 9, 29);
        var usuarioId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();
        db.Users.AddRange(
            new Usuario { Id = usuarioId, Nome = "Usuário", UserName = "usuario@teste.local" },
            new Usuario { Id = outroUsuarioId, Nome = "Outro", UserName = "outro@teste.local" });
        var ativo = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Ativo", CnpjCpf = "12345678000199" };
        var excluido = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Excluído", CnpjCpf = "98765432000198", ExcluidoEm = DateTime.UtcNow };
        db.Fornecedores.AddRange(ativo, excluido);
        db.ContratosFornecedor.AddRange(
            new ContratoFornecedor { Id = Guid.NewGuid(), FornecedorId = ativo.Id, Nome = "Renovação anual", VigenciaFim = hoje.AddDays(1) },
            new ContratoFornecedor { Id = Guid.NewGuid(), FornecedorId = ativo.Id, Nome = "Vence hoje", VigenciaFim = hoje },
            new ContratoFornecedor { Id = Guid.NewGuid(), FornecedorId = ativo.Id, Nome = "Vence depois", VigenciaFim = hoje.AddDays(2) },
            new ContratoFornecedor { Id = Guid.NewGuid(), FornecedorId = excluido.Id, Nome = "Fornecedor removido", VigenciaFim = hoje.AddDays(1) });
        db.ContasPagar.AddRange(
            NovaConta(ativo.Id, "Mensalidade", hoje.AddDays(1), StatusFinanceiro.EmAberto),
            NovaConta(ativo.Id, "Conta de hoje", hoje, StatusFinanceiro.EmAberto),
            NovaConta(ativo.Id, "Conta paga", hoje.AddDays(1), StatusFinanceiro.Paga),
            NovaConta(ativo.Id, "Conta cancelada", hoje.AddDays(1), StatusFinanceiro.Cancelada),
            NovaConta(ativo.Id, "Conta excluída", hoje.AddDays(1), StatusFinanceiro.EmAberto, DateTime.UtcNow),
            NovaConta(excluido.Id, "Fornecedor removido", hoje.AddDays(1), StatusFinanceiro.EmAberto));
        await db.SaveChangesAsync();

        var consulta = new ConsultaNotificacoes(db, new RelogioFixo(hoje));
        var notificacoes = await consulta.ListarAsync(usuarioId);

        Assert.Equal(2, notificacoes.Count);
        Assert.Contains(notificacoes, n => n.Tipo == TipoNotificacao.Contrato && n.Titulo == "Renovação anual");
        Assert.Contains(notificacoes, n => n.Tipo == TipoNotificacao.ContaPagar && n.Titulo == "Mensalidade");
        Assert.All(notificacoes, n =>
        {
            Assert.Equal("Fornecedor Ativo", n.Fornecedor);
            Assert.Equal(hoje.AddDays(1), n.Vencimento);
        });

        var conta = Assert.Single(notificacoes, n => n.Tipo == TipoNotificacao.ContaPagar);
        var resultado = await consulta.MarcarComoLidaAsync(usuarioId, conta.Tipo, conta.Id);

        Assert.True(resultado.Sucesso);
        Assert.Single(await consulta.ListarAsync(usuarioId));
        Assert.Equal(2, (await consulta.ListarAsync(outroUsuarioId)).Count);
        Assert.Single(db.NotificacoesLidas);
    }

    private static ContaPagar NovaConta(Guid fornecedorId, string descricao, DateOnly vencimento,
        StatusFinanceiro status, DateTime? excluidoEm = null) => new()
    {
        Id = Guid.NewGuid(),
        FornecedorId = fornecedorId,
        Descricao = descricao,
        Vencimento = vencimento,
        ValorOriginal = 100m,
        ValorFinal = 100m,
        StatusFinanceiro = status,
        CriadoPorId = Guid.NewGuid(),
        ExcluidoEm = excluidoEm,
    };
}
