using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Cartoes;

/// <summary>
/// Casos de uso de Cartão (Passo 13 do plano do MVP). Sem exclusão lógica
/// própria — "desativar" um cartão é <see cref="AlterarStatusAsync"/> pra
/// <see cref="StatusCartao.Cancelado"/>, não um ExcluirAsync separado.
/// </summary>
public interface IGerenciadorCartoes
{
    Task<Cartao> CriarAsync(CartaoInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarAsync(Guid id, CartaoInput input, Guid usuarioId);

    Task<ResultadoOperacao> AlterarStatusAsync(Guid id, StatusCartao novoStatus, Guid usuarioId);

    Task<IReadOnlyList<Cartao>> ListarAsync();
}
