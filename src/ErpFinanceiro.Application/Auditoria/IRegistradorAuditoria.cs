namespace ErpFinanceiro.Application.Auditoria;

/// <summary>
/// Contrato de auditoria usado pelos casos de uso desde o Passo 16 —
/// implementado de forma provisória (log simples, sem persistir em banco)
/// até o Passo 21 criar a entidade LogAuditoria de verdade e trocar a
/// implementação. Evita reabrir os módulos anteriores só para plugar
/// auditoria depois.
/// </summary>
public interface IRegistradorAuditoria
{
    Task RegistrarAsync(
        Guid usuarioId,
        string acao,
        string tipoEntidade,
        Guid entidadeId,
        object? valorAnterior,
        object? valorNovo);
}
