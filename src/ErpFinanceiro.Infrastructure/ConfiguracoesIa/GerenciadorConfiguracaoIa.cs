using System.Net.Http.Headers;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErpFinanceiro.Infrastructure.ConfiguracoesIa;

/// <summary>
/// Casos de uso da tela de configuração de IA. Scoped (nunca Singleton) de
/// propósito — cada leitura vem direto do banco, sem cache, pra trocar
/// provedor/modelo/chave valer na próxima chamada sem reiniciar o app.
/// O HttpClient injetado (typed client, ver Program.cs) é usado só pra
/// "Testar conexão" — chamadas ad-hoc com URI absoluta, sem BaseAddress.
/// </summary>
public sealed class GerenciadorConfiguracaoIa(
    AppDbContext db,
    IRegistradorAuditoria auditoria,
    UserManager<Usuario> userManager,
    HttpClient http,
    IConfiguration configuracao) : IGerenciadorConfiguracaoIa
{
    public Task<ErpFinanceiro.Domain.ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade) =>
        db.ConfiguracoesIa.FirstOrDefaultAsync(c => c.Finalidade == finalidade);

    public async Task<ResultadoOperacao> SalvarAsync(FinalidadeConfiguracaoIa finalidade, ConfiguracaoIaInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarAdministradorAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return ResultadoOperacao.Falha(erroPermissao);
        }

        if (string.IsNullOrWhiteSpace(input.Modelo))
        {
            return ResultadoOperacao.Falha("Informe o nome do modelo.");
        }

        var configuracaoExistente = await db.ConfiguracoesIa.FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        var chaveAlterada = !string.IsNullOrWhiteSpace(input.NovaApiKey);

        if (configuracaoExistente is null)
        {
            configuracaoExistente = new ErpFinanceiro.Domain.ConfiguracaoIa
            {
                Id = Guid.NewGuid(),
                Finalidade = finalidade,
            };
            db.ConfiguracoesIa.Add(configuracaoExistente);
        }

        configuracaoExistente.Provedor = input.Provedor;
        configuracaoExistente.Modelo = input.Modelo.Trim();
        configuracaoExistente.Ativo = input.Ativo;
        configuracaoExistente.TimeoutSegundos = input.TimeoutSegundos;
        configuracaoExistente.AtualizadoPorId = usuarioId;

        if (chaveAlterada)
        {
            configuracaoExistente.ApiKey = input.NovaApiKey;
        }

        await db.SaveChangesAsync();

        // Nunca loga a chave em si — só se ela mudou ou não.
        await auditoria.RegistrarAsync(usuarioId, "AlterarConfiguracaoIa", nameof(ErpFinanceiro.Domain.ConfiguracaoIa),
            configuracaoExistente.Id, null,
            new { Finalidade = finalidade.ToString(), configuracaoExistente.Provedor, configuracaoExistente.Modelo, configuracaoExistente.Ativo, ChaveAlterada = chaveAlterada });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade)
    {
        var config = await db.ConfiguracoesIa.FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        if (config is null || !config.Ativo)
        {
            return ResultadoOperacao.Falha("Configuração inativa ou não cadastrada.");
        }

        if (finalidade == FinalidadeConfiguracaoIa.Documentos)
        {
            return await TestarServicoDocumentosAsync();
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return ResultadoOperacao.Falha("Cadastre uma chave de API antes de testar.");
        }

        return await TestarProvedorAsync(config.Provedor, config.ApiKey);
    }

    private async Task<ResultadoOperacao> TestarServicoDocumentosAsync()
    {
        try
        {
            var baseUrl = configuracao["Ia:Leitor:BaseUrl"] ?? "http://localhost:8000";
            http.Timeout = TimeSpan.FromSeconds(10);
            var resposta = await http.GetAsync(new Uri(new Uri(baseUrl), "/health"));
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
            http.Timeout = TimeSpan.FromSeconds(10);

            HttpResponseMessage resposta = provedor switch
            {
                ProvedorIa.Gemini => await EnviarComHeaderGoogleAsync(http, "https://generativelanguage.googleapis.com/v1beta/models", apiKey),
                ProvedorIa.OpenAi => await EnviarComBearerAsync(http, "https://api.openai.com/v1/models", apiKey),
                ProvedorIa.Anthropic => await EnviarComAnthropicAsync(http, apiKey),
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
    private static Task<HttpResponseMessage> EnviarComHeaderGoogleAsync(HttpClient http, string url, string apiKey)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Add("x-goog-api-key", apiKey);
        return http.SendAsync(requisicao);
    }

    private static Task<HttpResponseMessage> EnviarComBearerAsync(HttpClient http, string url, string apiKey)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return http.SendAsync(requisicao);
    }

    private static Task<HttpResponseMessage> EnviarComAnthropicAsync(HttpClient http, string apiKey)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        requisicao.Headers.Add("x-api-key", apiKey);
        requisicao.Headers.Add("anthropic-version", "2023-06-01");
        return http.SendAsync(requisicao);
    }

    private async Task<string?> ValidarAdministradorAsync(Guid usuarioId)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return "Usuário não encontrado.";
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        return papeis.Contains(nameof(PerfilUsuario.Administrador))
            ? null
            : "Só usuários com perfil Administrador podem alterar a configuração de IA.";
    }
}
