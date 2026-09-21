using System.Net;
using System.Text;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.DocumentosIa;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErpFinanceiro.Tests.DocumentosIa;

public class LeitorDocumentosHttpTests
{
    private sealed class ConfiguracaoIaFalsa(ConfiguracaoIa? valor) : IGerenciadorConfiguracaoIa
    {
        public Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade) => Task.FromResult(valor);

        public Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade) =>
            throw new NotSupportedException();
    }

    /// <summary>Captura a última requisição feita e devolve uma resposta 200 canônica.</summary>
    private sealed class HandlerFalso : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaRequisicao { get; private set; }
        public string? UltimoCorpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaRequisicao = request;
            UltimoCorpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

            const string json = """{"tipoDetectado":"NotaFiscal","confiancaGeral":0.9,"campos":[]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static LeitorDocumentosHttp CriarLeitor(HandlerFalso handler, ConfiguracaoIa? config)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://servico-documentos.teste/") };
        return new LeitorDocumentosHttp(http, new ConfiguracaoIaFalsa(config), NullLogger<LeitorDocumentosHttp>.Instance);
    }

    private static Stream ArquivoFalso() => new MemoryStream(Encoding.UTF8.GetBytes("conteudo"));

    [Fact]
    public async Task LerAsync_sem_configuracao_ativa_nao_manda_override()
    {
        var handler = new HandlerFalso();
        var leitor = CriarLeitor(handler, config: null);

        var resultado = await leitor.LerAsync(ArquivoFalso(), "nf.pdf", "application/pdf");

        Assert.True(resultado.Sucesso);
        Assert.DoesNotContain("llm_provider", handler.UltimoCorpo);
        Assert.DoesNotContain("llm_api_key", handler.UltimoCorpo);
    }

    [Fact]
    public async Task LerAsync_com_configuracao_inativa_nao_manda_override()
    {
        var handler = new HandlerFalso();
        var config = new ConfiguracaoIa { Ativo = false, Provedor = ProvedorIa.Gemini, Modelo = "gemini-2.5-flash", ApiKey = "chave" };
        var leitor = CriarLeitor(handler, config);

        await leitor.LerAsync(ArquivoFalso(), "nf.pdf", "application/pdf");

        Assert.DoesNotContain("llm_provider", handler.UltimoCorpo);
    }

    [Fact]
    public async Task LerAsync_com_configuracao_ativa_manda_override_com_provedor_mapeado()
    {
        var handler = new HandlerFalso();
        var config = new ConfiguracaoIa { Ativo = true, Provedor = ProvedorIa.Gemini, Modelo = "gemini-2.5-flash", ApiKey = "minha-chave" };
        var leitor = CriarLeitor(handler, config);

        await leitor.LerAsync(ArquivoFalso(), "nf.pdf", "application/pdf");

        Assert.Contains("llm_provider", handler.UltimoCorpo);
        Assert.Contains("gemini", handler.UltimoCorpo);
        Assert.Contains("llm_model", handler.UltimoCorpo);
        Assert.Contains("gemini-2.5-flash", handler.UltimoCorpo);
        Assert.Contains("llm_api_key", handler.UltimoCorpo);
        Assert.Contains("minha-chave", handler.UltimoCorpo);
    }

    [Fact]
    public async Task LerAsync_com_configuracao_ativa_mas_sem_chave_nao_manda_override()
    {
        var handler = new HandlerFalso();
        var config = new ConfiguracaoIa { Ativo = true, Provedor = ProvedorIa.Gemini, Modelo = "gemini-2.5-flash", ApiKey = null };
        var leitor = CriarLeitor(handler, config);

        await leitor.LerAsync(ArquivoFalso(), "nf.pdf", "application/pdf");

        Assert.DoesNotContain("llm_provider", handler.UltimoCorpo);
    }
}
