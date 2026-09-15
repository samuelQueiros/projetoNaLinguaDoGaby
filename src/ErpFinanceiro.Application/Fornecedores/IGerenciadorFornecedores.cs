using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Fornecedores;

/// <summary>
/// Casos de uso de Fornecedor (Passo 10 do plano do MVP). Exclusão sempre
/// lógica (ExcluidoEm) — nunca DELETE físico.
/// </summary>
public interface IGerenciadorFornecedores
{
    Task<ResultadoCriacao<Fornecedor>> CriarAsync(CriarFornecedorInput input);

    Task<ResultadoOperacao> EditarAsync(Guid id, CriarFornecedorInput input);

    Task<ResultadoOperacao> ExcluirAsync(Guid id);

    Task<Fornecedor?> ObterAsync(Guid id);

    Task<IReadOnlyList<Fornecedor>> ListarAsync(bool incluirExcluidos = false);

    /// <summary>
    /// Adiciona um registro de dados bancários. Se <paramref name="input"/>
    /// tiver Principal = true, desmarca o principal atual do mesmo
    /// fornecedor (regra: só um principal por vez).
    /// </summary>
    Task<ResultadoOperacao> AdicionarDadosBancariosAsync(Guid fornecedorId, DadosBancariosInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarDadosBancariosAsync(Guid dadosBancariosId, DadosBancariosInput input, Guid usuarioId);

    Task<ResultadoOperacao> RemoverDadosBancariosAsync(Guid dadosBancariosId, Guid usuarioId);
}
