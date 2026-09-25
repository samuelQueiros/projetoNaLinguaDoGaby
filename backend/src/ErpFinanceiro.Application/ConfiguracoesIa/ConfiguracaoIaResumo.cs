using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ConfiguracoesIa;

/// <summary>
/// Retorno de <see cref="IGerenciadorConfiguracaoIa.ObterResumoAsync"/> —
/// alimenta a tela Configurações de IA. Nunca inclui o valor da chave de
/// API (só <see cref="ApiKeyDefinida"/>), mesmo quando ela vem de uma
/// variável de ambiente legível pelo processo: o objetivo é a tela não
/// colocar nenhuma chave em texto claro na resposta HTTP.
/// </summary>
public sealed record ConfiguracaoIaResumo(
    /// <summary>
    /// True quando existe uma linha salva pela tela para esta finalidade
    /// (mesmo que Ativo=false) — o valor exibido veio do banco, não da env
    /// var, e o botão "Restaurar padrão do ambiente" fica disponível.
    /// </summary>
    bool ConfiguradoPeloPainel,
    bool Ativo,
    ProvedorIa? Provedor,
    string? Modelo,
    int TimeoutSegundos,
    bool ApiKeyDefinida);
