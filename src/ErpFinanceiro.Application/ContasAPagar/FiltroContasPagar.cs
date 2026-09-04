using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ContasAPagar;

public sealed record FiltroContasPagar(
    Guid? FornecedorId = null,
    StatusFinanceiro? StatusFinanceiro = null,
    StatusAprovacao? StatusAprovacao = null,
    DateOnly? VencimentoInicial = null,
    DateOnly? VencimentoFinal = null,
    bool IncluirExcluidas = false);
