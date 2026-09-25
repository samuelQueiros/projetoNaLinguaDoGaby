using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ConfiguracoesIa;

/// <summary>
/// Entrada de <see cref="IGerenciadorConfiguracaoIa.SalvarAsync"/>. ApiKey
/// nulo/vazio significa "manter a chave já salva" quando já existe uma
/// linha para a finalidade — o formulário da tela nunca pré-preenche a
/// chave atual (ela não é devolvida em texto claro, ver
/// <see cref="ConfiguracaoIaResumo"/>), então reenviar em branco não pode
/// apagá-la sem querer. Uma configuração nova (primeira vez) exige chave.
/// </summary>
public sealed record SalvarConfiguracaoIaInput(
    bool Ativo,
    ProvedorIa Provedor,
    string Modelo,
    string? ApiKey,
    int TimeoutSegundos);
