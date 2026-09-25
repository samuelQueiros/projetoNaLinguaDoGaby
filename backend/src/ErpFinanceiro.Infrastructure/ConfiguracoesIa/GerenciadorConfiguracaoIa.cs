using System.Net.Http.Headers;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErpFinanceiro.Infrastructure.ConfiguracoesIa;

/// <summary>
/// Configuração de IA: uma linha salva pela tela Configurações de IA
/// (Administrador, tabela ConfiguracoesIa — <see cref="ConfiguracaoIaSalva"/>,
/// ApiKey cifrada em repouso) prevalece quando existe; sem ela, cai para a
/// variável de ambiente <c>Ia:Documentos</c>/<c>Ia:Chat</c> (env vars
/// <c>Ia__Documentos__*</c>/<c>Ia__Chat__*</c>) — permite subir o ambiente
/// funcionando via docker-compose/.env sem precisar abrir a tela, e depois
/// editar por ali sem redeploy. <see cref="ObterAsync"/> nunca cacheia em
/// campo (sempre relê banco/config), pra uma edição valer no próximo
/// request. O HttpClient injetado (typed client, ver Program.cs) é usado só
/// pra "Testar conexão" — chamadas ad-hoc com URI absoluta, sem BaseAddress.
/// </summary>
public sealed class GerenciadorConfiguracaoIa(
    HttpClient http,
    IConfiguration configuracao,
    AppDbContext db,
    IRegistradorAuditoria auditoria) : IGerenciadorConfiguracaoIa
{
    public async Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade)
    {
        var salva = await db.ConfiguracoesIa.AsNoTracking().FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        if (salva is not null)
        {
            // Config salva pela tela existe mas está incompleta (não deveria
            // acontecer — SalvarAsync valida antes de gravar — mas defensivo
            // contra edição direta no banco): trata como "não configurado",
            // não cai para a env var (o admin optou por gerenciar pela tela).
            return string.IsNullOrWhiteSpace(salva.Modelo) || string.IsNullOrWhiteSpace(salva.ApiKey)
                ? null
                : new ConfiguracaoIa
                {
                    Ativo = salva.Ativo,
                    Provedor = salva.Provedor,
                    Modelo = salva.Modelo,
                    ApiKey = salva.ApiKey,
                    TimeoutSegundos = salva.TimeoutSegundos,
                };
        }

        return ObterDaEnv(finalidade);
    }

    public async Task<ConfiguracaoIaResumo> ObterResumoAsync(FinalidadeConfiguracaoIa finalidade)
    {
        var salva = await db.ConfiguracoesIa.AsNoTracking().FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        if (salva is not null)
        {
            return new ConfiguracaoIaResumo(
                ConfiguradoPeloPainel: true,
                Ativo: salva.Ativo,
                Provedor: salva.Provedor,
                Modelo: salva.Modelo,
                TimeoutSegundos: salva.TimeoutSegundos,
                ApiKeyDefinida: !string.IsNullOrWhiteSpace(salva.ApiKey));
        }

        // Sem linha salva: mostra o que está efetivamente valendo via env
        // var, só pra a tela informar a situação atual — salvar a partir
        // daqui cria a linha (deixa de depender da env var).
        var daEnv = ObterDaEnv(finalidade);
        return new ConfiguracaoIaResumo(
            ConfiguradoPeloPainel: false,
            Ativo: daEnv?.Ativo ?? false,
            Provedor: daEnv?.Provedor,
            Modelo: daEnv?.Modelo,
            TimeoutSegundos: daEnv?.TimeoutSegundos ?? 90,
            ApiKeyDefinida: !string.IsNullOrWhiteSpace(daEnv?.ApiKey));
    }

    public async Task<ResultadoOperacao> SalvarAsync(FinalidadeConfiguracaoIa finalidade, SalvarConfiguracaoIaInput input, Guid usuarioId)
    {
        if (string.IsNullOrWhiteSpace(input.Modelo))
        {
            return ResultadoOperacao.Falha("Informe o modelo.");
        }

        if (input.TimeoutSegundos <= 0)
        {
            return ResultadoOperacao.Falha("Timeout deve ser maior que zero.");
        }

        var salva = await db.ConfiguracoesIa.FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        var existia = salva is not null;

        if (!existia && string.IsNullOrWhiteSpace(input.ApiKey))
        {
            return ResultadoOperacao.Falha("Informe a chave de API.");
        }

        var anterior = existia
            ? new { salva!.Ativo, salva.Provedor, salva.Modelo, salva.TimeoutSegundos, ApiKeyDefinida = !string.IsNullOrWhiteSpace(salva.ApiKey) }
            : null;

        salva ??= new ConfiguracaoIaSalva { Id = Guid.NewGuid(), Finalidade = finalidade };

        salva.Ativo = input.Ativo;
        salva.Provedor = input.Provedor;
        salva.Modelo = input.Modelo.Trim();
        salva.TimeoutSegundos = input.TimeoutSegundos;
        salva.AtualizadoPorId = usuarioId;
        // ApiKey em branco = manter a atual (só existe caminho de troca, não
        // de "esvaziar" — ver doc de SalvarConfiguracaoIaInput).
        if (!string.IsNullOrWhiteSpace(input.ApiKey))
        {
            salva.ApiKey = input.ApiKey;
        }

        if (!existia)
        {
            db.ConfiguracoesIa.Add(salva);
        }

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, existia ? "Editar" : "Criar", nameof(ConfiguracaoIaSalva), salva.Id, anterior,
            new { salva.Ativo, salva.Provedor, salva.Modelo, salva.TimeoutSegundos, ApiKeyDefinida = !string.IsNullOrWhiteSpace(salva.ApiKey) });

        return ResultadoOperacao.Ok();
    }

    public async Task RestaurarPadraoAsync(FinalidadeConfiguracaoIa finalidade, Guid usuarioId)
    {
        var salva = await db.ConfiguracoesIa.FirstOrDefaultAsync(c => c.Finalidade == finalidade);
        if (salva is null)
        {
            return;
        }

        db.ConfiguracoesIa.Remove(salva);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Remover", nameof(ConfiguracaoIaSalva), salva.Id,
            new { salva.Ativo, salva.Provedor, salva.Modelo, salva.TimeoutSegundos }, null);
    }

    private ConfiguracaoIa? ObterDaEnv(FinalidadeConfiguracaoIa finalidade)
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
            return null;
        }

        // Ativo é opcional (default true quando a chave está presente) —
        // existe só pra desligar temporariamente um provedor já configurado
        // sem precisar apagar a chave da env var.
        var ativo = !bool.TryParse(secao["Ativo"], out var ativoConfigurado) || ativoConfigurado;
        var timeout = int.TryParse(secao["TimeoutSegundos"], out var timeoutConfigurado) ? timeoutConfigurado : 90;

        return new ConfiguracaoIa
        {
            Ativo = ativo,
            Provedor = provedor,
            Modelo = modelo,
            ApiKey = apiKey,
            TimeoutSegundos = timeout,
        };
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
                $"Configuração ausente, incompleta ou inativa (tela Configurações de IA ou variáveis de ambiente Ia__{finalidade}__Provedor/Modelo/ApiKey).");
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
