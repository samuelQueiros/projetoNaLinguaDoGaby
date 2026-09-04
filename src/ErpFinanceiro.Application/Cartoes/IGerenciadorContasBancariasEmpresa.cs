using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Cartoes;

/// <summary>
/// CRUD de contas bancárias da empresa (Passo 13). Não há pasta dedicada
/// "ContasBancarias" na seção 4 do CLAUDE.md — colocado em Cartoes por ser
/// o módulo mais próximo (ambos entram no mesmo passo do plano).
/// </summary>
public interface IGerenciadorContasBancariasEmpresa
{
    Task<ContaBancariaEmpresa> CriarAsync(ContaBancariaEmpresaInput input);

    Task<ResultadoOperacao> EditarAsync(Guid id, ContaBancariaEmpresaInput input);

    Task<ResultadoOperacao> InativarAsync(Guid id);

    Task<ResultadoOperacao> ReativarAsync(Guid id);

    Task<IReadOnlyList<ContaBancariaEmpresa>> ListarAsync(bool apenasAtivas = false);
}
