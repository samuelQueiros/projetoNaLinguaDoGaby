using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// Registro de pagamentos (parcial ou total) e estorno (Passo 19 do plano
/// do MVP). Recalcula StatusFinanceiro da conta a cada operação.
/// </summary>
public interface IGerenciadorPagamentos
{
    Task<ResultadoOperacao> RegistrarAsync(Guid contaPagarId, RegistrarPagamentoInput input, Guid usuarioId);

    /// <summary>Ação crítica — motivo obrigatório.</summary>
    Task<ResultadoOperacao> EstornarAsync(Guid pagamentoId, string motivo, Guid usuarioId);

    Task<IReadOnlyList<Pagamento>> ListarPorContaAsync(Guid contaPagarId);
}
