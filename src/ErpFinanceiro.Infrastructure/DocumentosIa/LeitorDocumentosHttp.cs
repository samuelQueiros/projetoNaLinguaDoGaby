using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.DocumentosIa;

/// <summary>
/// Implementação de <see cref="ILeitorDocumentos"/> que chama o serviço
/// Python (docling + LLM) via HTTP — <c>POST {BaseUrl}/extrair</c> com o
/// arquivo em multipart. Contrato em docs/modulo-ia-documentos.md.
///
/// Registrada só quando <c>Ia:Leitor:Modo = Http</c>; caso contrário o
/// <see cref="LeitorDocumentosStub"/> assume. O <c>HttpClient</c> (BaseAddress
/// e Timeout) é configurado no Program.cs via <c>AddHttpClient</c>.
///
/// A cada chamada lê <see cref="IGerenciadorConfiguracaoIa"/> (finalidade
/// Documentos) e, se estiver ativa e com chave cadastrada, manda
/// llm_provider/llm_model/llm_api_key como override — o Administrador troca
/// provedor/modelo/chave em /configuracoes-ia e vale na próxima chamada,
/// sem reiniciar este serviço nem o Python. Sem override ativo, o serviço
/// Python cai no próprio .env (ver servico-documentos/app/main.py).
/// </summary>
public sealed class LeitorDocumentosHttp(
    HttpClient http,
    IGerenciadorConfiguracaoIa configuracaoIa,
    ILogger<LeitorDocumentosHttp> logger) : ILeitorDocumentos
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ResultadoLeituraDocumento> LerAsync(
        Stream conteudo,
        string nomeArquivo,
        string tipoConteudo,
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var arquivoContent = new StreamContent(conteudo);
            arquivoContent.Headers.ContentType =
                MediaTypeHeaderValue.TryParse(tipoConteudo, out var mt) ? mt : new MediaTypeHeaderValue("application/octet-stream");
            form.Add(arquivoContent, "arquivo", nomeArquivo);

            var config = await configuracaoIa.ObterAsync(FinalidadeConfiguracaoIa.Documentos);
            if (config is { Ativo: true } && !string.IsNullOrWhiteSpace(config.ApiKey))
            {
                form.Add(new StringContent(MapearProvedor(config.Provedor)), "llm_provider");
                form.Add(new StringContent(config.Modelo), "llm_model");
                form.Add(new StringContent(config.ApiKey), "llm_api_key");
            }

            using var resposta = await http.PostAsync("extrair", form, ct);

            if (resposta.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                return ResultadoLeituraDocumento.Falha("Documento ilegível para o serviço de leitura.");
            }

            resposta.EnsureSuccessStatusCode();

            var payload = await resposta.Content.ReadFromJsonAsync<ExtracaoResposta>(JsonOpts, ct);
            if (payload is null)
            {
                return ResultadoLeituraDocumento.Falha("Resposta vazia do serviço de leitura.");
            }

            var tipo = Enum.TryParse<TipoDocumentoDetectado>(payload.TipoDetectado, ignoreCase: true, out var t)
                ? t
                : TipoDocumentoDetectado.NaoIdentificado;

            var campos = (payload.Campos ?? [])
                .Select(c => new CampoLido(c.Nome ?? string.Empty, c.Valor, c.Confianca))
                .ToList();

            return new ResultadoLeituraDocumento(tipo, payload.ConfiancaGeral, campos);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Falha ao chamar o serviço de leitura de documentos para '{Arquivo}'.", nomeArquivo);
            return ResultadoLeituraDocumento.Falha($"Serviço de leitura indisponível: {ex.Message}");
        }
    }

    private static string MapearProvedor(ProvedorIa provedor) => provedor switch
    {
        ProvedorIa.Gemini => "gemini",
        ProvedorIa.OpenAi => "openai",
        ProvedorIa.Anthropic => "anthropic",
        _ => throw new NotSupportedException($"Provedor não suportado pelo serviço de documentos: {provedor}"),
    };

    private sealed record ExtracaoResposta(
        [property: JsonPropertyName("tipoDetectado")] string? TipoDetectado,
        [property: JsonPropertyName("confiancaGeral")] decimal ConfiancaGeral,
        [property: JsonPropertyName("campos")] List<CampoResposta>? Campos);

    private sealed record CampoResposta(
        [property: JsonPropertyName("nome")] string? Nome,
        [property: JsonPropertyName("valor")] string? Valor,
        [property: JsonPropertyName("confianca")] decimal Confianca);
}
