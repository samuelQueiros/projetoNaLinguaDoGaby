using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Cartoes;

public interface IGerenciadorCartoes
{
    Task<Cartao> CriarAsync(CartaoInput input);

    Task<ResultadoOperacao> EditarAsync(Guid id, CartaoInput input);

    Task<ResultadoOperacao> AlterarStatusAsync(Guid id, StatusCartao novoStatus);

    Task<IReadOnlyList<Cartao>> ListarAsync();
}
