using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Categorias;

/// <summary>
/// Casos de uso de Criar/Editar/Listar/Inativar reutilizados por qualquer
/// cadastro simples de nome/descrição/ativo (Passo 6 do plano do MVP) —
/// hoje Categoria e CentroCusto, ambas <see cref="ICadastroSimples"/>.
/// Exclusão é sempre lógica: nunca há um método de exclusão física.
/// </summary>
public interface IGerenciadorCadastroSimples<TEntidade>
    where TEntidade : class, ICadastroSimples
{
    Task<TEntidade> CriarAsync(string nome, string? descricao);

    Task<ResultadoOperacao> EditarAsync(Guid id, string nome, string? descricao);

    /// <summary>Exclusão lógica: Ativo = false, nunca remove a linha.</summary>
    Task<ResultadoOperacao> InativarAsync(Guid id);

    Task<ResultadoOperacao> ReativarAsync(Guid id);

    Task<IReadOnlyList<TEntidade>> ListarAsync(bool apenasAtivos = false);
}
