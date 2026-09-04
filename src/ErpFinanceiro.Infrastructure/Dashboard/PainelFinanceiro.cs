using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Dashboard;

public sealed class PainelFinanceiro(AppDbContext db, IRelogio relogio) : IPainelFinanceiro
{
    public async Task<IndicadoresPainel> ObterAsync()
    {
        var hoje = relogio.Hoje();
        var em7Dias = hoje.AddDays(7);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);
        var inicioAno = new DateOnly(hoje.Year, 1, 1);

        var abertas = db.ContasPagar.AsNoTracking()
            .Where(c => c.ExcluidoEm == null
                && c.StatusFinanceiro != StatusFinanceiro.Paga
                && c.StatusFinanceiro != StatusFinanceiro.Cancelada);

        var totalEmAberto = await abertas.SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;
        var quantidadeEmAberto = await abertas.CountAsync();

        var vencidas = abertas.Where(c => c.Vencimento < hoje);
        var totalVencidas = await vencidas.SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;
        var quantidadeVencidas = await vencidas.CountAsync();

        var totalHoje = await abertas.Where(c => c.Vencimento == hoje).SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;

        var total7Dias = await abertas
            .Where(c => c.Vencimento >= hoje && c.Vencimento <= em7Dias)
            .SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;

        var totalNoMes = await abertas
            .Where(c => c.Vencimento >= inicioMes && c.Vencimento <= fimMes)
            .SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;

        var pagamentosConfirmados = db.Pagamentos.AsNoTracking()
            .Where(p => p.Status == StatusPagamento.Confirmado);

        var totalPagoNoMes = await pagamentosConfirmados
            .Where(p => p.Data >= inicioMes && p.Data <= fimMes)
            .SumAsync(p => (decimal?)p.ValorPago) ?? 0m;

        var totalPagoNoAno = await pagamentosConfirmados
            .Where(p => p.Data >= inicioAno)
            .SumAsync(p => (decimal?)p.ValorPago) ?? 0m;

        // Proxy: ainda não existe CartaoDespesa (controle de despesas
        // previstas no cartão, com parcelas — seção 5 do escopo), então o
        // "gasto no cartão" hoje só pode ser lido dos pagamentos já
        // efetivamente registrados com CartaoId preenchido.
        var totalCartaoNoAno = await pagamentosConfirmados
            .Where(p => p.CartaoId != null && p.Data >= inicioAno)
            .SumAsync(p => (decimal?)p.ValorPago) ?? 0m;

        var naoIdentificadas = db.ContasPagar.AsNoTracking()
            .Where(c => c.ExcluidoEm == null && c.StatusFinanceiro == StatusFinanceiro.PagamentoNaoIdentificado);
        var totalNaoIdentificado = await naoIdentificadas.SumAsync(c => (decimal?)c.ValorFinal) ?? 0m;
        var quantidadeNaoIdentificado = await naoIdentificadas.CountAsync();

        var proximosVencimentos = await abertas
            .Include(c => c.Fornecedor)
            .OrderBy(c => c.Vencimento)
            .Take(10)
            .Select(c => new VencimentoProximo(c.Id, c.Fornecedor!.RazaoSocial, c.Descricao, c.Vencimento, c.ValorFinal))
            .ToListAsync();

        return new IndicadoresPainel(
            totalEmAberto, quantidadeEmAberto,
            totalVencidas, quantidadeVencidas,
            totalHoje, total7Dias, totalNoMes,
            totalPagoNoMes, totalPagoNoAno,
            totalCartaoNoAno,
            totalNaoIdentificado, quantidadeNaoIdentificado,
            proximosVencimentos);
    }
}
