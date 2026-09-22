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
/// Documentos, vinda das variáveis de ambiente <c>Ia__Documentos__*</c>) e,
/// se estiver ativa e com chave cadastrada, manda
/// llm_provider/llm_model/llm_api_key como override — trocar a env var e
/// reiniciar o container do "web" já vale na próxima chamada, sem reiniciar
/// o Python. Sem override ativo, o serviço Python cai no próprio .env (ver
/// servico-documentos/app/main.py).
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

            // Mensagens distintas por causa (item "tratamento de erros" do
            // escopo) — cada status do contrato HTTP (ver README do serviço
            // Python) vira uma frase que ajuda quem revisa a decidir o que
            // fazer, sem expor corpo/stack do serviço interno (log tem o
            // detalhe completo).
            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await LerCorpoParaLogAsync(resposta, ct);
                logger.LogError(
                    "Serviço de leitura de documentos respondeu {Status} para '{Arquivo}': {Corpo}",
                    (int)resposta.StatusCode, nomeArquivo, corpo);

                return resposta.StatusCode switch
                {
                    System.Net.HttpStatusCode.UnprocessableEntity =>
                        ResultadoLeituraDocumento.Falha("Documento ilegível — não encontrei texto aproveitável (PDF escaneado sem OCR, imagem corrompida ou arquivo vazio)."),
                    System.Net.HttpStatusCode.UnsupportedMediaType =>
                        ResultadoLeituraDocumento.Falha("Formato de arquivo não suportado pela leitura automática."),
                    System.Net.HttpStatusCode.RequestEntityTooLarge =>
                        ResultadoLeituraDocumento.Falha("Arquivo excede o tamanho máximo aceito pela leitura automática."),
                    System.Net.HttpStatusCode.BadRequest =>
                        ResultadoLeituraDocumento.Falha("Não consegui ler este documento — configuração de leitura inválida."),
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                        ResultadoLeituraDocumento.Falha("O serviço de leitura de documentos recusou a chamada — problema de configuração interna, avise um Administrador."),
                    _ => ResultadoLeituraDocumento.Falha("O serviço de leitura de documentos falhou ao processar este arquivo — tente de novo em instantes."),
                };
            }

            var payload = await resposta.Content.ReadFromJsonAsync<ExtracaoResposta>(JsonOpts, ct);
            if (payload is null)
            {
                logger.LogError("Resposta vazia (200 sem corpo JSON) do serviço de leitura para '{Arquivo}'.", nomeArquivo);
                return ResultadoLeituraDocumento.Falha("O serviço de leitura devolveu uma resposta inesperada — tente de novo em instantes.");
            }

            var tipo = Enum.TryParse<TipoDocumentoDetectado>(payload.TipoDetectado, ignoreCase: true, out var t)
                ? t
                : TipoDocumentoDetectado.NaoIdentificado;

            var campos = (payload.Campos ?? [])
                .Select(c => new CampoLido(c.Nome ?? string.Empty, c.Valor, c.Confianca))
                .ToList();

            return new ResultadoLeituraDocumento(tipo, payload.ConfiancaGeral, campos, Texto: payload.Texto);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // ex.Message aqui pode conter host/porta internos (ex.: "target
            // machine actively refused it (servico-documentos:8000)") — só
            // no log, nunca pro usuário (mesmo raciocínio já aplicado ao
            // catch-all de GerenciadorDocumentosImportados.ProcessarAsync).
            logger.LogError(ex, "Falha de comunicação com o serviço de leitura de documentos para '{Arquivo}'.", nomeArquivo);
            return ResultadoLeituraDocumento.Falha("Não consegui falar com o serviço de leitura de documentos agora — tente de novo em instantes.");
        }
    }

    private static async Task<string> LerCorpoParaLogAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            return await resposta.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            return "(corpo indisponível)";
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
        [property: JsonPropertyName("campos")] List<CampoResposta>? Campos,
        [property: JsonPropertyName("texto")] string? Texto);

    private sealed record CampoResposta(
        [property: JsonPropertyName("nome")] string? Nome,
        [property: JsonPropertyName("valor")] string? Valor,
        [property: JsonPropertyName("confianca")] decimal Confianca);
}
