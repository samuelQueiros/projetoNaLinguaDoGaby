using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// <see cref="Pagina"/>/<see cref="TamanhoPagina"/> só valem para
/// <see cref="IGerenciadorContasPagar.ListarPaginadoAsync"/> — o
/// <see cref="IGerenciadorContasPagar.ListarAsync"/> "clássico" (usado pelo
/// dashboard, pela tela de Relatórios e pelas exportações) continua
/// devolvendo tudo que bate no filtro, sem paginar.
/// </summary>
public sealed record FiltroContasPagar(
    Guid? FornecedorId = null,
    StatusFinanceiro? StatusFinanceiro = null,
    StatusAprovacao? StatusAprovacao = null,
    DateOnly? VencimentoInicial = null,
    DateOnly? VencimentoFinal = null,
    bool IncluirExcluidas = false,
    int Pagina = 1,
    int TamanhoPagina = 20);
