using ErpFinanceiro.Application.ChatIa;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Infrastructure.ChatIa;

/// <summary>
/// Implementação de <see cref="IAgenteChatIa"/> registrada no DI — lê a
/// <see cref="ConfiguracaoIa"/> (finalidade Chat) A CADA chamada (nunca
/// cacheada) e delega pro adapter do provedor configurado. Hoje só Gemini
/// está implementado; adicionar OpenAI/Anthropic depois é só escrever
/// outro adapter com a mesma forma de <see cref="AgenteChatIaGemini"/> e
/// acrescentar um `case` aqui — o catálogo de ferramentas e o resto do
/// sistema não mudam.
/// </summary>
public sealed class AgenteChatIaFactory(
    IGerenciadorConfiguracaoIa configuracaoIa,
    AgenteChatIaGemini geminiAgente) : IAgenteChatIa
{
    public async Task<RespostaChatIa> ResponderAsync(
        IReadOnlyList<MensagemChatIa> historico,
        string mensagemUsuario,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var config = await configuracaoIa.ObterAsync(FinalidadeConfiguracaoIa.Chat);
        if (config is not { Ativo: true } || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return RespostaChatIa.Falha(
                "o chat de IA ainda não foi configurado. Peça para um Administrador configurar em Configurações de IA.");
        }

        return config.Provedor switch
        {
            ProvedorIa.Gemini => await geminiAgente.ResponderAsync(
                historico, mensagemUsuario, usuarioId, config.Modelo, config.ApiKey, config.TimeoutSegundos, ct),
            _ => RespostaChatIa.Falha($"o provedor '{config.Provedor}' ainda não é suportado no chat."),
        };
    }
}
