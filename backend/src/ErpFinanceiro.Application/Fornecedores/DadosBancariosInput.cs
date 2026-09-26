using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Fornecedores;

public sealed record DadosBancariosInput(
    string Banco,
    string Agencia,
    string Conta,
    TipoContaBancaria Tipo,
    string NomeTitular,
    string CpfCnpjTitular,
    string? ChavePix,
    bool Principal);
