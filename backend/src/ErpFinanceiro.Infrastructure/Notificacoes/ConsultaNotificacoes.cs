using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Notificacoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Notificacoes;

public sealed class ConsultaNotificacoes(AppDbContext db, IRelogio relogio) : IConsultaNotificacoes
{
    public async Task<IReadOnlyList<NotificacaoVencimento>> ListarAsync(Guid usuarioId)
    {
        var vencimento = relogio.Hoje().AddDays(1);

        var contratos = await db.ContratosFornecedor.AsNoTracking()
            .Where(c => c.VigenciaFim == vencimento
                && c.Fornecedor!.ExcluidoEm == null
                && !db.NotificacoesLidas.Any(n => n.UsuarioId == usuarioId
                    && n.Tipo == TipoNotificacao.Contrato
                    && n.ReferenciaId == c.Id
                    && n.Vencimento == c.VigenciaFim))
            .Select(c => new NotificacaoVencimento(
                c.Id,
                c.FornecedorId,
                TipoNotificacao.Contrato,
                c.Nome,
                c.Fornecedor!.RazaoSocial,
                c.VigenciaFim))
            .ToListAsync();

        var contas = await db.ContasPagar.AsNoTracking()
            .Where(c => c.Vencimento == vencimento
                && c.ExcluidoEm == null
                && c.Fornecedor!.ExcluidoEm == null
                && c.StatusFinanceiro != StatusFinanceiro.Paga
                && c.StatusFinanceiro != StatusFinanceiro.Cancelada
                && !db.NotificacoesLidas.Any(n => n.UsuarioId == usuarioId
                    && n.Tipo == TipoNotificacao.ContaPagar
                    && n.ReferenciaId == c.Id
                    && n.Vencimento == c.Vencimento))
            .Select(c => new NotificacaoVencimento(
                c.Id,
                c.FornecedorId,
                TipoNotificacao.ContaPagar,
                c.Descricao,
                c.Fornecedor!.RazaoSocial,
                c.Vencimento))
            .ToListAsync();

        return contratos.Concat(contas)
            .OrderBy(n => n.Tipo)
            .ThenBy(n => n.Fornecedor)
            .ThenBy(n => n.Titulo)
            .ToList();
    }

    public async Task<ResultadoOperacao> MarcarComoLidaAsync(Guid usuarioId, TipoNotificacao tipo, Guid referenciaId)
    {
        var vencimentoEsperado = relogio.Hoje().AddDays(1);
        var existe = tipo switch
        {
            TipoNotificacao.Contrato => await db.ContratosFornecedor.AnyAsync(c => c.Id == referenciaId
                && c.VigenciaFim == vencimentoEsperado && c.Fornecedor!.ExcluidoEm == null),
            TipoNotificacao.ContaPagar => await db.ContasPagar.AnyAsync(c => c.Id == referenciaId
                && c.Vencimento == vencimentoEsperado && c.ExcluidoEm == null
                && c.Fornecedor!.ExcluidoEm == null
                && c.StatusFinanceiro != StatusFinanceiro.Paga
                && c.StatusFinanceiro != StatusFinanceiro.Cancelada),
            _ => false,
        };

        if (!existe)
            return ResultadoOperacao.Falha("Notificação não encontrada ou não está mais ativa.");

        var jaLida = await db.NotificacoesLidas.AnyAsync(n => n.UsuarioId == usuarioId
            && n.Tipo == tipo && n.ReferenciaId == referenciaId && n.Vencimento == vencimentoEsperado);
        if (jaLida)
            return ResultadoOperacao.Ok();

        db.NotificacoesLidas.Add(new NotificacaoLida
        {
            UsuarioId = usuarioId,
            Tipo = tipo,
            ReferenciaId = referenciaId,
            Vencimento = vencimentoEsperado,
            LidaEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }
}
