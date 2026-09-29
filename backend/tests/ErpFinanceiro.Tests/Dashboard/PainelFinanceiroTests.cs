using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Dashboard;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Dashboard;

public class PainelFinanceiroTests
{
    private sealed class RelogioFixo(DateOnly hoje) : IRelogio
    {
        public DateOnly Hoje() => hoje;
    }

    private static async Task<(AppDbContext Db, Guid FornecedorId, Guid UsuarioId)> CriarBaseAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Painel", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        var usuarioId = Guid.NewGuid();
        await db.SaveChangesAsync();
        return (db, fornecedor.Id, usuarioId);
    }

    private static ContaPagar NovaConta(Guid fornecedorId, Guid usuarioId, DateOnly vencimento, decimal valor,
        StatusFinanceiro status = StatusFinanceiro.EmAberto) => new()
    {
        Id = Guid.NewGuid(),
        FornecedorId = fornecedorId,
        Descricao = "Conta teste",
        Vencimento = vencimento,
        ValorOriginal = valor,
        ValorFinal = valor,
        StatusFinanceiro = status,
        CriadoPorId = usuarioId,
    };

    [Fact]
    public async Task ObterAsync_calcula_totais_em_aberto_vencidas_e_por_periodo()
    {
        var (db, fornecedorId, usuarioId) = await CriarBaseAsync();
        var hoje = new DateOnly(2026, 6, 15);

        db.ContasPagar.AddRange(
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(-5), 100m), // vencida
            NovaConta(fornecedorId, usuarioId, hoje, 200m), // hoje
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(3), 300m), // próximos 7 dias
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(20), 400m), // mês seguinte
            NovaConta(fornecedorId, usuarioId, hoje.AddMonths(2), 500m), // fora do mês
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(-1), 999m, StatusFinanceiro.Paga)); // paga, não conta
        await db.SaveChangesAsync();

        var painel = new PainelFinanceiro(db, new RelogioFixo(hoje));
        var indicadores = await painel.ObterAsync();

        Assert.Equal(1500m, indicadores.TotalEmAberto); // 100+200+300+400+500
        Assert.Equal(5, indicadores.QuantidadeEmAberto);
        Assert.Equal(100m, indicadores.TotalVencidas);
        Assert.Equal(1, indicadores.QuantidadeVencidas);
        Assert.Equal(200m, indicadores.TotalAPagarHoje);
        Assert.Equal(500m, indicadores.TotalAPagarProximos7Dias); // hoje + em 3 dias
        Assert.Equal(600m, indicadores.TotalAPagarNoMes); // vencida + hoje + próximos 7 dias, todas em junho/2026
    }

    [Fact]
    public async Task ObterAsync_soma_pagamentos_confirmados_do_mes_e_do_ano()
    {
        var (db, fornecedorId, usuarioId) = await CriarBaseAsync();
        var hoje = new DateOnly(2026, 6, 15);

        var conta1 = NovaConta(fornecedorId, usuarioId, hoje, 100m, StatusFinanceiro.Paga);
        var conta2 = NovaConta(fornecedorId, usuarioId, hoje, 200m, StatusFinanceiro.Paga);
        db.ContasPagar.AddRange(conta1, conta2);
        db.Pagamentos.AddRange(
            new Pagamento { Id = Guid.NewGuid(), ContaPagarId = conta1.Id, Data = hoje, ValorPago = 100m, Status = StatusPagamento.Confirmado, RegistradoPorId = usuarioId },
            new Pagamento { Id = Guid.NewGuid(), ContaPagarId = conta2.Id, Data = new DateOnly(2026, 2, 1), ValorPago = 200m, Status = StatusPagamento.Confirmado, RegistradoPorId = usuarioId },
            new Pagamento { Id = Guid.NewGuid(), ContaPagarId = conta1.Id, Data = hoje, ValorPago = 999m, Status = StatusPagamento.Estornado, RegistradoPorId = usuarioId });
        await db.SaveChangesAsync();

        var painel = new PainelFinanceiro(db, new RelogioFixo(hoje));
        var indicadores = await painel.ObterAsync();

        Assert.Equal(100m, indicadores.TotalPagoNoMes);
        Assert.Equal(300m, indicadores.TotalPagoNoAno);
    }

    [Fact]
    public async Task ObterAsync_agrupa_gasto_por_centro_de_custo()
    {
        var (db, fornecedorId, usuarioId) = await CriarBaseAsync();
        var hoje = new DateOnly(2026, 6, 15);
        var marketing = new CentroCusto { Id = Guid.NewGuid(), Nome = "Marketing", Cor = "#eb6834" };
        var ti = new CentroCusto { Id = Guid.NewGuid(), Nome = "TI" };
        db.CentrosDeCusto.AddRange(marketing, ti);

        var semCentro = NovaConta(fornecedorId, usuarioId, hoje, 100m);
        var doMarketing = NovaConta(fornecedorId, usuarioId, hoje, 300m);
        doMarketing.CentroCustoId = marketing.Id;
        var doTi = NovaConta(fornecedorId, usuarioId, hoje, 200m);
        doTi.CentroCustoId = ti.Id;
        var doTiPaga = NovaConta(fornecedorId, usuarioId, hoje, 50m, StatusFinanceiro.Paga);
        doTiPaga.CentroCustoId = ti.Id;
        var cancelada = NovaConta(fornecedorId, usuarioId, hoje, 999m, StatusFinanceiro.Cancelada);
        cancelada.CentroCustoId = ti.Id;
        db.ContasPagar.AddRange(semCentro, doMarketing, doTi, doTiPaga, cancelada);
        await db.SaveChangesAsync();

        var painel = new PainelFinanceiro(db, new RelogioFixo(hoje));
        var indicadores = await painel.ObterAsync();

        Assert.Equal(3, indicadores.GastoPorCentroCusto.Count);
        Assert.Equal(300m, indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "Marketing").Total);
        Assert.Equal("#eb6834", indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "Marketing").Cor);
        Assert.Equal(250m, indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "TI").Total); // 200 + 50 paga; cancelada não entra
        Assert.Null(indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "TI").Cor); // sem cor definida
        Assert.Equal(100m, indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "Sem centro de custo").Total);
        Assert.Null(indicadores.GastoPorCentroCusto.Single(g => g.CentroCusto == "Sem centro de custo").Cor);
    }

    [Fact]
    public async Task ObterAsync_lista_proximos_vencimentos_ordenados()
    {
        var (db, fornecedorId, usuarioId) = await CriarBaseAsync();
        var hoje = new DateOnly(2026, 6, 15);

        db.ContasPagar.AddRange(
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(10), 100m),
            NovaConta(fornecedorId, usuarioId, hoje.AddDays(1), 200m));
        await db.SaveChangesAsync();

        var painel = new PainelFinanceiro(db, new RelogioFixo(hoje));
        var indicadores = await painel.ObterAsync();

        Assert.Equal(2, indicadores.ProximosVencimentos.Count);
        Assert.Equal(200m, indicadores.ProximosVencimentos[0].ValorFinal);
    }

    [Fact]
    public async Task ObterContasPorMesAsync_separa_contas_a_pagar_e_pagamentos_confirmados()
    {
        var (db, fornecedorId, usuarioId) = await CriarBaseAsync();
        var janeiro = new DateOnly(2026, 1, 10);
        var fevereiro = new DateOnly(2026, 2, 10);
        var abertaJaneiro = NovaConta(fornecedorId, usuarioId, janeiro, 100m);
        var abertaFevereiro = NovaConta(fornecedorId, usuarioId, fevereiro, 200m);
        var paga = NovaConta(fornecedorId, usuarioId, janeiro, 300m, StatusFinanceiro.Paga);
        var cancelada = NovaConta(fornecedorId, usuarioId, fevereiro, 900m, StatusFinanceiro.Cancelada);
        db.ContasPagar.AddRange(abertaJaneiro, abertaFevereiro, paga, cancelada);
        db.Pagamentos.AddRange(
            new Pagamento { Id = Guid.NewGuid(), ContaPagarId = paga.Id, Data = janeiro, ValorPago = 300m, Status = StatusPagamento.Confirmado, RegistradoPorId = usuarioId },
            new Pagamento { Id = Guid.NewGuid(), ContaPagarId = paga.Id, Data = fevereiro, ValorPago = 999m, Status = StatusPagamento.Estornado, RegistradoPorId = usuarioId });
        await db.SaveChangesAsync();

        var painel = new PainelFinanceiro(db, new RelogioFixo(janeiro));
        var aPagar = await painel.ObterContasPorMesAsync(TipoGraficoContas.APagar, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        var pagas = await painel.ObterContasPorMesAsync(TipoGraficoContas.Pagas, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(3, aPagar.Count);
        Assert.Equal(100m, aPagar[0].Total);
        Assert.Equal(200m, aPagar[1].Total);
        Assert.Equal(0m, aPagar[2].Total);
        Assert.Equal(300m, pagas[0].Total);
        Assert.Equal(0m, pagas[1].Total);
        Assert.Equal(1, pagas[0].Quantidade);
    }
}
