namespace ErpFinanceiro.Application.Cartoes;

public sealed record CartaoInput(
    string InstituicaoFinanceira,
    string Bandeira,
    string Apelido,
    string UltimosQuatroDigitos,
    decimal Limite,
    int DiaFechamento,
    int DiaVencimento,
    Guid ResponsavelId);
