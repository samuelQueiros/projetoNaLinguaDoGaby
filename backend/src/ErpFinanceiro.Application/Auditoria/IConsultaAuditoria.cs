namespace ErpFinanceiro.Application.Auditoria;

/// <summary>Uma linha do log de auditoria (seção 15 do escopo — "log de atividades").</summary>
public sealed record RegistroAuditoria(
    DateTime Data,
    string UsuarioNome,
    string Acao,
    string TipoEntidade,
    Guid EntidadeId,
    string? ValorAnteriorJson,
    string? ValorNovoJson);

public sealed record FiltroLogAuditoria(
    string? TipoEntidade = null,
    DateTime? DataInicial = null,
    DateTime? DataFinal = null);

/// <summary>
/// Consulta geral do log de auditoria (módulo 10 do plano do MVP) — a
/// timeline por entidade (<see cref="ITimelineConsulta"/>) já existia;
/// esta é a listagem/tela de log completa (seção 15 do escopo).
/// </summary>
public interface IConsultaAuditoria
{
    Task<IReadOnlyList<RegistroAuditoria>> ListarAsync(FiltroLogAuditoria filtro, int limite = 200);

    Task<IReadOnlyList<string>> ListarTiposEntidadeAsync();
}
