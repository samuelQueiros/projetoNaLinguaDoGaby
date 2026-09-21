using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ErpFinanceiro.Application.ChatIa;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.ChatIa;

/// <summary>
/// Chama a API do Gemini (REST direto — não existe SDK .NET oficial do
/// Google equivalente ao `google-genai` do Python, e um HttpClient com o
/// contrato documentado é mais simples de manter que uma lib de terceiro
/// de procedência incerta) com function calling pro catálogo fixo de
/// ferramentas. O formato exato da requisição/resposta foi conferido
/// empiricamente contra o SDK oficial (capturando o corpo HTTP que ele
/// manda de verdade), não só documentação.
///
/// PROMPT DE SISTEMA — a segunda maior alavanca (depois do catálogo de
/// ferramentas em si) pra funcionar bem com modelo fraco: regras
/// obrigatórias explícitas (nunca inventar dado, nunca agir, sempre citar
/// a ferramenta), não "seja útil e preciso" genérico.
/// </summary>
public sealed class AgenteChatIaGemini(HttpClient http, ExecutorFerramentasChatIa executor, ILogger<AgenteChatIaGemini> logger)
{
    private const int MaxChamadasFerramenta = 3;

    private const string SistemaPrompt = """
        Você é o assistente financeiro do ERP — ajuda a equipe financeira, gestores e administradores a
        consultar dados do sistema por texto, através de um pequeno conjunto de ferramentas de consulta.

        REGRAS OBRIGATÓRIAS (nunca quebre nenhuma):
        1. Você SÓ pode responder com base em dados que vieram de uma chamada de ferramenta. Nunca invente,
           estime ou "chute" um número, nome, data ou status que não veio de uma ferramenta.
        2. Se a pergunta pedir um dado que nenhuma ferramenta cobre, diga isso claramente e sugira a tela do
           sistema onde a pessoa provavelmente encontra a informação — não tente adivinhar de outro jeito.
        3. Você NÃO pode criar, editar, aprovar, rejeitar, cancelar ou excluir nada. Se pedirem uma ação
           (ex.: "aprova essa conta", "muda o vencimento", "cadastra um fornecedor"), explique com educação
           que isso precisa ser feito na tela correspondente do sistema — você só consulta, nunca executa.
        4. Ao usar dados de uma ferramenta, seja específico e cite exatamente o que veio: valores em reais
           (R$ 1.234,56), datas em dd/mm/aaaa, nomes como estão cadastrados.
        5. Se uma consulta não retornar nada, diga isso claramente ("não encontrei nenhuma conta com esse
           filtro") em vez de inventar uma resposta genérica ou insistir tentando outros filtros sem sentido.
        6. Seja direto e objetivo. Respostas curtas e úteis — isto é um chat, não um relatório.
        7. Se um nome de fornecedor citado não for encontrado, diga isso e pergunte se a pessoa quis dizer
           outro nome — não assuma qual fornecedor é.
        """;

    public async Task<RespostaChatIa> ResponderAsync(
        IReadOnlyList<MensagemChatIa> historico,
        string mensagemUsuario,
        Guid usuarioId,
        string modelo,
        string apiKey,
        int timeoutSegundos,
        CancellationToken ct)
    {
        try
        {
            http.Timeout = TimeSpan.FromSeconds(timeoutSegundos);

            var contents = new JsonArray();
            foreach (var m in historico)
            {
                contents.Add(ConteudoDeTexto(m.Papel, m.Texto));
            }

            contents.Add(ConteudoDeTexto("user", mensagemUsuario));

            for (var rodada = 0; rodada < MaxChamadasFerramenta; rodada++)
            {
                var payload = new JsonObject
                {
                    ["contents"] = contents.DeepClone(),
                    ["systemInstruction"] = new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray { new JsonObject { ["text"] = SistemaPrompt } },
                    },
                    ["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = ConstruirDeclaracoes() } },
                    ["generationConfig"] = new JsonObject { ["temperature"] = 0 },
                };

                // A chave vai no header x-goog-api-key, não na query string —
                // o HttpClient registrado via AddHttpClient tem um handler de
                // log padrão que grava método+URI (com query string) em nível
                // Information, e essa categoria não está suprimida em
                // appsettings.json. Com a chave na URL ela vazava em texto
                // plano no log a cada mensagem do chat (achado crítico da
                // auditoria de segurança); headers não são logados nesse
                // nível, só em Trace.
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(modelo)}:generateContent";
                using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(payload),
                };
                requisicao.Headers.Add("x-goog-api-key", apiKey);
                using var resposta = await http.SendAsync(requisicao, ct);

                if (!resposta.IsSuccessStatusCode)
                {
                    var corpo = await resposta.Content.ReadAsStringAsync(ct);
                    logger.LogError("Gemini respondeu {Status} no chat: {Corpo}", (int)resposta.StatusCode, corpo);

                    // Só o código HTTP no retorno pro usuário (não o corpo, que
                    // pode conter detalhe interno do provedor) — o suficiente
                    // pra um Administrador diagnosticar (401/403 = chave errada
                    // ou sem permissão, 404 = nome de modelo errado, 429 =
                    // cota excedida) sem precisar ir atrás do log do servidor.
                    return RespostaChatIa.Falha(
                        $"o serviço de IA recusou a requisição (HTTP {(int)resposta.StatusCode}) — verifique a chave/modelo em Configurações de IA.");
                }

                var dto = await resposta.Content.ReadFromJsonAsync<RespostaGeminiDto>(JsonOpts, ct);
                var parte = dto?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault();
                if (parte is null)
                {
                    return RespostaChatIa.Falha("o serviço de IA devolveu uma resposta vazia.");
                }

                if (parte.FunctionCall is { Name: not null } chamada)
                {
                    contents.Add(new JsonObject
                    {
                        ["role"] = "model",
                        ["parts"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["functionCall"] = new JsonObject
                                {
                                    ["name"] = chamada.Name,
                                    ["args"] = chamada.Args.ValueKind == JsonValueKind.Undefined
                                        ? new JsonObject()
                                        : JsonNode.Parse(chamada.Args.GetRawText()),
                                },
                            },
                        },
                    });

                    var resultado = await executor.ExecutarAsync(chamada.Name, chamada.Args, usuarioId, ct);

                    contents.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["functionResponse"] = new JsonObject
                                {
                                    ["name"] = chamada.Name,
                                    ["response"] = JsonNode.Parse(JsonSerializer.Serialize(resultado, JsonOpts)),
                                },
                            },
                        },
                    });

                    continue; // próxima rodada: manda o resultado da ferramenta de volta pro modelo
                }

                if (!string.IsNullOrWhiteSpace(parte.Text))
                {
                    return RespostaChatIa.Ok(parte.Text.Trim());
                }

                return RespostaChatIa.Falha("o serviço de IA não devolveu texto nem chamada de ferramenta.");
            }

            return RespostaChatIa.Falha("não consegui concluir a resposta (muitas consultas encadeadas nesta pergunta).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Falha ao chamar o Gemini para o chat.");
            return RespostaChatIa.Falha("não consegui falar com o serviço de IA agora — tente de novo em instantes.");
        }
    }

    private static JsonObject ConteudoDeTexto(string papel, string texto) => new()
    {
        ["role"] = papel,
        ["parts"] = new JsonArray { new JsonObject { ["text"] = texto } },
    };

    private static JsonArray ConstruirDeclaracoes()
    {
        var array = new JsonArray();
        foreach (var f in CatalogoFerramentasChatIa.Todas)
        {
            var declaracao = new JsonObject
            {
                ["name"] = f.Nome,
                ["description"] = f.Descricao,
            };

            if (f.Parametros.Count > 0)
            {
                var propriedades = new JsonObject();
                foreach (var p in f.Parametros)
                {
                    propriedades[p.Nome] = new JsonObject
                    {
                        ["type"] = MapearTipo(p.Tipo),
                        ["description"] = p.Descricao,
                    };
                }

                declaracao["parameters"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = propriedades,
                };
            }

            array.Add(declaracao);
        }

        return array;
    }

    private static string MapearTipo(string tipo) => tipo switch
    {
        "string" => "STRING",
        "number" => "NUMBER",
        "boolean" => "BOOLEAN",
        _ => "STRING",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record RespostaGeminiDto(
        [property: JsonPropertyName("candidates")] List<CandidatoDto>? Candidates);

    private sealed record CandidatoDto(
        [property: JsonPropertyName("content")] ConteudoDto? Content);

    private sealed record ConteudoDto(
        [property: JsonPropertyName("parts")] List<ParteDto>? Parts);

    private sealed record ParteDto(
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("functionCall")] FunctionCallDto? FunctionCall);

    private sealed record FunctionCallDto(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("args")] JsonElement Args);
}
