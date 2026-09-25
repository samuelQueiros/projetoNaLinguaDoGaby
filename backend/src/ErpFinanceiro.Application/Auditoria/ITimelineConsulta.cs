namespace ErpFinanceiro.Application.Auditoria;

/// <summary>
/// A timeline de uma conta a pagar (seção 17 do escopo) é uma consulta
/// filtrada em LogAuditoria por TipoEntidade+EntidadeId (decisão 2, seção 6
/// do CLAUDE.md) — não uma tabela separada.
/// </summary>
public interface ITimelineConsulta
{
    Task<IReadOnlyList<EventoTimeline>> ObterAsync(string tipoEntidade, Guid entidadeId);
}
