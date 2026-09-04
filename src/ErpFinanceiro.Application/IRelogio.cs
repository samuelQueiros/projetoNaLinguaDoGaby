namespace ErpFinanceiro.Application;

/// <summary>
/// Abstração de "hoje" para regras de negócio que dependem da data atual
/// (recálculo de StatusFinanceiro, dashboard) — evita DateTime.Now/UtcNow
/// espalhado pelo código, e permite fixar "hoje" em testes (recomendação
/// do db-schema-reviewer, Passo 14).
/// </summary>
public interface IRelogio
{
    DateOnly Hoje();
}
