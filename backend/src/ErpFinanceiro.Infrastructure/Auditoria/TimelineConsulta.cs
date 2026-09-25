using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Auditoria;

public sealed class TimelineConsulta(AppDbContext db) : ITimelineConsulta
{
    public async Task<IReadOnlyList<EventoTimeline>> ObterAsync(string tipoEntidade, Guid entidadeId) =>
        await db.LogsAuditoria
            .AsNoTracking()
            .Where(l => l.TipoEntidade == tipoEntidade && l.EntidadeId == entidadeId)
            .OrderBy(l => l.Data)
            .Select(l => new EventoTimeline(l.Data, l.Acao, l.Usuario != null ? l.Usuario.Nome : "—"))
            .ToListAsync();
}
