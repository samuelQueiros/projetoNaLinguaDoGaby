namespace ErpFinanceiro.Application.Fornecedores;

public sealed record CriarFornecedorInput(
    string RazaoSocial,
    string? NomeFantasia,
    string CnpjCpf,
    string? InscricaoEstadual,
    string? Endereco,
    string? Telefone,
    string? Email,
    string? ContatoResponsavel,
    Guid? FormaPagamentoPadraoId,
    string? Observacoes);
