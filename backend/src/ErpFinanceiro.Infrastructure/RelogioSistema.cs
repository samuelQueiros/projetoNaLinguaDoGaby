using ErpFinanceiro.Application;

namespace ErpFinanceiro.Infrastructure;

public sealed class RelogioSistema : IRelogio
{
    public DateOnly Hoje() => DateOnly.FromDateTime(DateTime.UtcNow);
}
