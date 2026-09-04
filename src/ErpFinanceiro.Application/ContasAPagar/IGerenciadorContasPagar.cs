using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// CRUD de ContaPagar (Passo 16 do plano do MVP) — sem aprovação/pagamento
/// ainda (Passos 18-19). Editar valor/fornecedor/vencimento é considerado
/// "ação crítica" (seção 8 do CLAUDE.md); a confirmação em si é
/// responsabilidade da UI (Passo 17), não deste caso de uso.
/// </summary>
public interface IGerenciadorContasPagar
{
    Task<ResultadoContaPagar> CriarAsync(ContaPagarInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarAsync(Guid id, ContaPagarInput input, Guid usuarioId);

    /// <summary>Exclusão lógica (ExcluidoEm) — nunca remove a linha.</summary>
    Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId);

    Task<ContaPagar?> ObterAsync(Guid id);

    Task<IReadOnlyList<ContaPagar>> ListarAsync(FiltroContasPagar filtro);
}
