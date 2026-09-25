using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Auditoria;

public sealed class ConsultaAuditoria(AppDbContext db) : IConsultaAuditoria
{
    public async Task<IReadOnlyList<RegistroAuditoria>> ListarAsync(FiltroLogAuditoria filtro, int limite = 200)
    {
        var query = db.LogsAuditoria.AsNoTracking().Include(l => l.Usuario).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.TipoEntidade))
        {
            query = query.Where(l => l.TipoEntidade == filtro.TipoEntidade);
        }

        if (filtro.DataInicial is DateTime inicio)
        {
            query = query.Where(l => l.Data >= inicio);
        }

        if (filtro.DataFinal is DateTime fim)
        {
            query = query.Where(l => l.Data <= fim);
        }

        return await query
            .OrderByDescending(l => l.Data)
            .Take(limite)
            .Select(l => new RegistroAuditoria(
                l.Data,
                l.Usuario != null ? l.Usuario.Nome : "—",
                l.Acao,
                l.TipoEntidade,
                l.EntidadeId,
                l.ValorAnteriorJson,
                l.ValorNovoJson))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> ListarTiposEntidadeAsync() =>
        await db.LogsAuditoria.AsNoTracking()
            .Select(l => l.TipoEntidade)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
}
