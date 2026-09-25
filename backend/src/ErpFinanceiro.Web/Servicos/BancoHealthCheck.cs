using ErpFinanceiro.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErpFinanceiro.Web.Servicos;

public sealed class BancoHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Banco indisponível.");
    }
}
