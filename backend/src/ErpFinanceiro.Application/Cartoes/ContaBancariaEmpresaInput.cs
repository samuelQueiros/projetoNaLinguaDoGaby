using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Cartoes;

public sealed record ContaBancariaEmpresaInput(
    string Banco,
    string Agencia,
    string Conta,
    TipoContaBancaria Tipo,
    string Apelido);
