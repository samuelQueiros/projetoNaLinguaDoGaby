using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Cartoes;

/// <summary>
/// CRUD de contas bancárias da empresa (Passo 13). Não há pasta dedicada
/// "ContasBancarias" na seção 4 do CLAUDE.md — colocado em Cartoes por ser
/// o módulo mais próximo (ambos entram no mesmo passo do plano).
/// </summary>
public interface IGerenciadorContasBancariasEmpresa
{
    Task<ContaBancariaEmpresa> CriarAsync(ContaBancariaEmpresaInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarAsync(Guid id, ContaBancariaEmpresaInput input, Guid usuarioId);

    Task<ResultadoOperacao> InativarAsync(Guid id, Guid usuarioId);

    Task<ResultadoOperacao> ReativarAsync(Guid id, Guid usuarioId);

    Task<IReadOnlyList<ContaBancariaEmpresa>> ListarAsync(bool apenasAtivas = false);
}
