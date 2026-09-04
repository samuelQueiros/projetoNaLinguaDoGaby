using ErpFinanceiro.Application.Auditoria;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.Auditoria;

/// <summary>
/// Implementação provisória de <see cref="IRegistradorAuditoria"/> — só
/// loga, não persiste. Trocada pela implementação real no Passo 21, quando
/// a entidade LogAuditoria existir.
/// </summary>
public sealed class RegistradorAuditoriaProvisorio(ILogger<RegistradorAuditoriaProvisorio> logger) : IRegistradorAuditoria
{
    public Task RegistrarAsync(
        Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo)
    {
        logger.LogInformation(
            "[Auditoria provisória] Usuario={UsuarioId} Acao={Acao} Entidade={TipoEntidade}#{EntidadeId}",
            usuarioId, acao, tipoEntidade, entidadeId);
        return Task.CompletedTask;
    }
}
