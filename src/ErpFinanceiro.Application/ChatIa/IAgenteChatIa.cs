namespace ErpFinanceiro.Application.ChatIa;

/// <summary>Uma mensagem do histórico da conversa. Papel: "user" ou "model".</summary>
public sealed record MensagemChatIa(string Papel, string Texto);

/// <summary>Resposta do agente pra um turno de conversa.</summary>
public sealed record RespostaChatIa(string Texto, bool Sucesso, string? Erro = null)
{
    public static RespostaChatIa Ok(string texto) => new(texto, true);

    public static RespostaChatIa Falha(string erro) => new(
        "Não consegui responder agora. " + erro, false, erro);
}

/// <summary>
/// Agente de chat — responde perguntas em linguagem natural sobre dados do
/// ERP usando um catálogo fixo de ferramentas somente-leitura (ver
/// <see cref="CatalogoFerramentasChatIa"/>). NUNCA cria, edita, aprova ou
/// exclui nada — é só consulta. Provider-agnóstico por design: a
/// implementação (Gemini, e no futuro OpenAI/Anthropic) fica só na
/// Infrastructure, escolhida via <see cref="ConfiguracaoIa"/> (finalidade
/// Chat) — trocar de provedor não deveria exigir mudar esta interface.
/// </summary>
public interface IAgenteChatIa
{
    Task<RespostaChatIa> ResponderAsync(
        IReadOnlyList<MensagemChatIa> historico,
        string mensagemUsuario,
        Guid usuarioId,
        CancellationToken ct = default);
}
