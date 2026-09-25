using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Fornecedores;

public sealed record DadosBancariosInput(
    string Banco,
    string Agencia,
    string Conta,
    TipoContaBancaria Tipo,
    string? ChavePix,
    bool Principal);
