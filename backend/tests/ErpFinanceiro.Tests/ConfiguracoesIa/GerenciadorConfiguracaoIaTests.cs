using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ConfiguracoesIa;
using Microsoft.Extensions.Configuration;

namespace ErpFinanceiro.Tests.ConfiguracoesIa;

public class GerenciadorConfiguracaoIaTests
{
    private static GerenciadorConfiguracaoIa Criar(params (string Chave, string Valor)[] configuracoes)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(configuracoes.Select(c => new KeyValuePair<string, string?>(c.Chave, c.Valor)))
            .Build();

        return new GerenciadorConfiguracaoIa(new HttpClient(), configuracao);
    }

    [Fact]
    public async Task ObterAsync_com_variaveis_completas_devolve_configuracao_ativa()
    {
        var gerenciador = Criar(
            ("Ia:Chat:Provedor", "Gemini"),
            ("Ia:Chat:Modelo", "gemini-2.5-flash"),
            ("Ia:Chat:ApiKey", "chave-teste"));

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.NotNull(config);
        Assert.True(config!.Ativo);
        Assert.Equal(ProvedorIa.Gemini, config.Provedor);
        Assert.Equal("gemini-2.5-flash", config.Modelo);
        Assert.Equal("chave-teste", config.ApiKey);
        Assert.Equal(90, config.TimeoutSegundos); // default quando TimeoutSegundos não é setado
    }

    [Fact]
    public async Task ObterAsync_sem_nenhuma_variavel_devolve_null()
    {
        var gerenciador = Criar();

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.Null(config);
    }

    [Theory]
    [InlineData("Ia:Chat:Provedor")]
    [InlineData("Ia:Chat:Modelo")]
    [InlineData("Ia:Chat:ApiKey")]
    public async Task ObterAsync_com_variavel_obrigatoria_faltando_devolve_null(string chaveFaltante)
    {
        var completas = new Dictionary<string, string>
        {
            ["Ia:Chat:Provedor"] = "Gemini",
            ["Ia:Chat:Modelo"] = "gemini-2.5-flash",
            ["Ia:Chat:ApiKey"] = "chave-teste",
        };
        completas.Remove(chaveFaltante);

        var gerenciador = Criar(completas.Select(c => (c.Key, c.Value)).ToArray());

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.Null(config);
    }

    [Fact]
    public async Task ObterAsync_com_provedor_nao_reconhecido_devolve_null()
    {
        var gerenciador = Criar(
            ("Ia:Chat:Provedor", "NaoExiste"),
            ("Ia:Chat:Modelo", "algum-modelo"),
            ("Ia:Chat:ApiKey", "chave"));

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.Null(config);
    }

    [Fact]
    public async Task ObterAsync_com_ativo_false_explicito_desliga_mesmo_com_chave_presente()
    {
        var gerenciador = Criar(
            ("Ia:Chat:Provedor", "Gemini"),
            ("Ia:Chat:Modelo", "gemini-2.5-flash"),
            ("Ia:Chat:ApiKey", "chave-teste"),
            ("Ia:Chat:Ativo", "false"));

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.NotNull(config);
        Assert.False(config!.Ativo);
    }

    [Fact]
    public async Task ObterAsync_respeita_timeout_customizado()
    {
        var gerenciador = Criar(
            ("Ia:Documentos:Provedor", "OpenAi"),
            ("Ia:Documentos:Modelo", "gpt-4o-mini"),
            ("Ia:Documentos:ApiKey", "chave"),
            ("Ia:Documentos:TimeoutSegundos", "120"));

        var config = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Documentos);

        Assert.Equal(120, config!.TimeoutSegundos);
    }

    [Fact]
    public async Task ObterAsync_duas_finalidades_sao_independentes()
    {
        var gerenciador = Criar(
            ("Ia:Documentos:Provedor", "Gemini"),
            ("Ia:Documentos:Modelo", "gemini-2.5-flash"),
            ("Ia:Documentos:ApiKey", "chave-doc"),
            ("Ia:Chat:Provedor", "Anthropic"),
            ("Ia:Chat:Modelo", "claude-opus-5"),
            ("Ia:Chat:ApiKey", "chave-chat"));

        var documentos = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Documentos);
        var chat = await gerenciador.ObterAsync(FinalidadeConfiguracaoIa.Chat);

        Assert.Equal(ProvedorIa.Gemini, documentos!.Provedor);
        Assert.Equal(ProvedorIa.Anthropic, chat!.Provedor);
    }
}
