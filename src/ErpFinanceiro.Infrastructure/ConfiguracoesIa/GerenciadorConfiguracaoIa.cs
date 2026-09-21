using System.Net.Http.Headers;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Domain;
using Microsoft.Extensions.Configuration;

namespace ErpFinanceiro.Infrastructure.ConfiguracoesIa;

/// <summary>
/// Lê a configuração de IA de variáveis de ambiente — seção
/// <c>Ia:Documentos</c> ou <c>Ia:Chat</c> (env vars <c>Ia__Documentos__*</c> /
/// <c>Ia__Chat__*</c>: Provedor, Modelo, ApiKey, TimeoutSegundos, e o
/// opcional Ativo pra desligar sem apagar a chave). Nada é lido do banco —
/// <see cref="IConfiguration"/> é consultado a cada chamada (nunca cacheado
/// num campo), então trocar a env var e reiniciar o container já é
/// suficiente pra valer no próximo request, sem migração nem tela de
/// cadastro. O HttpClient injetado (typed client, ver Program.cs) é usado
/// só pra "Testar conexão" — chamadas ad-hoc com URI absoluta, sem
/// BaseAddress.
///
/// Configuração ausente ou incompleta (falta Provedor, Modelo ou ApiKey, ou
/// Provedor não reconhecido) é tratada como "não configurado" —
/// <see cref="ObterAsync"/> devolve null, igual a antes quando a linha do
/// banco não existia.
/// </summary>
public sealed class GerenciadorConfiguracaoIa(HttpClient http, IConfiguration configuracao) : IGerenciadorConfiguracaoIa
{
    public Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade)
    {
        var secao = configuracao.GetSection($"Ia:{finalidade}");

        var provedorTexto = secao["Provedor"];
        var modelo = secao["Modelo"];
        var apiKey = secao["ApiKey"];

        if (string.IsNullOrWhiteSpace(provedorTexto)
            || string.IsNullOrWhiteSpace(modelo)
            || string.IsNullOrWhiteSpace(apiKey)
            || !Enum.TryParse<ProvedorIa>(provedorTexto, ignoreCase: true, out var provedor))
        {
            return Task.FromResult<ConfiguracaoIa?>(null);
        }

        // Ativo é opcional (default true quando a chave está presente) —
        // existe só pra desligar temporariamente um provedor já configurado
        // sem precisar apagar a chave da env var.
        var ativo = !bool.TryParse(secao["Ativo"], out var ativoConfigurado) || ativoConfigurado;
        var timeout = int.TryParse(secao["TimeoutSegundos"], out var timeoutConfigurado) ? timeoutConfigurado : 90;

        return Task.FromResult<ConfiguracaoIa?>(new ConfiguracaoIa
        {
            Ativo = ativo,
            Provedor = provedor,
            Modelo = modelo,
            ApiKey = apiKey,
            TimeoutSegundos = timeout,
        });
    }

    public async Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade)
    {
        if (finalidade == FinalidadeConfiguracaoIa.Documentos)
        {
            var resultadoServico = await TestarServicoDocumentosAsync();
            if (!resultadoServico.Sucesso)
            {
                return resultadoServico;
            }
        }

        var config = await ObterAsync(finalidade);
        if (config is not { Ativo: true })
        {
            return ResultadoOperacao.Falha(
                $"Configuração ausente, incompleta ou inativa nas variáveis de ambiente (Ia__{finalidade}__Provedor/Modelo/ApiKey).");
        }

        return await TestarProvedorAsync(config.Provedor, config.ApiKey!);
    }

    private async Task<ResultadoOperacao> TestarServicoDocumentosAsync()
    {
        try
        {
            var baseUrl = configuracao["Ia:Leitor:BaseUrl"] ?? "http://localhost:8000";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resposta = await http.GetAsync(new Uri(new Uri(baseUrl), "/health"), cts.Token);
            return resposta.IsSuccessStatusCode
                ? ResultadoOperacao.Ok()
                : ResultadoOperacao.Falha($"Serviço de documentos respondeu {(int)resposta.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ResultadoOperacao.Falha($"Não foi possível alcançar o serviço de documentos: {ex.Message}");
        }
    }

    private async Task<ResultadoOperacao> TestarProvedorAsync(ProvedorIa provedor, string apiKey)
    {
        try
        {
            // NÃO usar http.Timeout aqui — este HttpClient (typed client) é
            // capturado uma vez pela página/componente e reaproveitado a
            // cada clique em "Testar conexão"; Timeout só pode ser setado
            // antes da primeira requisição enviada, e no segundo clique
            // lançaria InvalidOperationException. O timeout entra via
            // CancellationToken (mesma correção de AgenteChatIaGemini).
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            HttpResponseMessage resposta = provedor switch
            {
                ProvedorIa.Gemini => await EnviarComHeaderGoogleAsync(http, "https://generativelanguage.googleapis.com/v1beta/models", apiKey, cts.Token),
                ProvedorIa.OpenAi => await EnviarComBearerAsync(http, "https://api.openai.com/v1/models", apiKey, cts.Token),
                ProvedorIa.Anthropic => await EnviarComAnthropicAsync(http, apiKey, cts.Token),
                _ => throw new NotSupportedException($"Provedor não suportado: {provedor}"),
            };

            return resposta.IsSuccessStatusCode
                ? ResultadoOperacao.Ok()
                : ResultadoOperacao.Falha($"Provedor respondeu {(int)resposta.StatusCode} — verifique a chave/modelo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ResultadoOperacao.Falha($"Não foi possível conectar ao provedor: {ex.Message}");
        }
    }

    /// <summary>
    /// Chave no header x-goog-api-key, não na query string (?key=...) — a
    /// API do Gemini aceita as duas formas, mas a query string acaba
    /// gravada em texto plano pelo handler de log padrão do HttpClient
    /// (método+URI em nível Information). Mesma correção aplicada em
    /// AgenteChatIaGemini (achado crítico da auditoria de segurança).
    /// </summary>
    private static Task<HttpResponseMessage> EnviarComHeaderGoogleAsync(HttpClient http, string url, string apiKey, CancellationToken ct)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Add("x-goog-api-key", apiKey);
        return http.SendAsync(requisicao, ct);
    }

    private static Task<HttpResponseMessage> EnviarComBearerAsync(HttpClient http, string url, string apiKey, CancellationToken ct)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return http.SendAsync(requisicao, ct);
    }

    private static Task<HttpResponseMessage> EnviarComAnthropicAsync(HttpClient http, string apiKey, CancellationToken ct)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        requisicao.Headers.Add("x-api-key", apiKey);
        requisicao.Headers.Add("anthropic-version", "2023-06-01");
        return http.SendAsync(requisicao, ct);
    }
}
