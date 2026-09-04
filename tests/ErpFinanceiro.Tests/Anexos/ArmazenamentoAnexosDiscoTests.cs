using System.Text;
using ErpFinanceiro.Infrastructure.Storage;

namespace ErpFinanceiro.Tests.Anexos;

public class ArmazenamentoAnexosDiscoTests : IDisposable
{
    private readonly string raiz = Path.Combine(Path.GetTempPath(), "erp-anexos-teste-" + Guid.NewGuid().ToString("N"));

    private ArmazenamentoAnexosDisco Criar(Action<OpcoesArmazenamentoAnexos>? ajustar = null)
    {
        var opcoes = new OpcoesArmazenamentoAnexos { RaizFisica = raiz };
        ajustar?.Invoke(opcoes);
        return new ArmazenamentoAnexosDisco(opcoes);
    }

    private static MemoryStream Conteudo(string texto) => new(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public async Task SalvarAsync_grava_com_nome_fisico_gerado_e_recupera_byte_a_byte()
    {
        var storage = Criar();

        var armazenado = await storage.SalvarAsync(Conteudo("conteúdo do PDF"), "Nota Fiscal 123.pdf", "application/pdf");

        // Nome do usuário nunca aparece no caminho físico.
        Assert.DoesNotContain("Nota Fiscal", armazenado.CaminhoRelativo);
        Assert.EndsWith(".pdf", armazenado.CaminhoRelativo);

        await using var lido = await storage.AbrirAsync(armazenado.CaminhoRelativo);
        using var reader = new StreamReader(lido);
        Assert.Equal("conteúdo do PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task SalvarAsync_extensao_fora_da_allowlist_e_recusada()
    {
        var storage = Criar();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SalvarAsync(Conteudo("MZ..."), "malware.exe", "application/octet-stream"));
    }

    [Fact]
    public async Task SalvarAsync_acima_do_tamanho_maximo_e_recusado_e_nao_deixa_lixo()
    {
        var storage = Criar(o => o.TamanhoMaximoBytes = 4);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SalvarAsync(Conteudo("muito maior que quatro bytes"), "a.txt", "text/plain"));

        Assert.False(Directory.Exists(raiz) && Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories).Any());
    }

    [Theory]
    [InlineData("../escapou.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("sub/../../fora.pdf")]
    public async Task AbrirAsync_com_path_traversal_e_recusado(string caminho)
    {
        var storage = Criar();

        await Assert.ThrowsAnyAsync<Exception>(() => storage.AbrirAsync(caminho));
    }

    [Fact]
    public async Task ExcluirAsync_remove_o_arquivo()
    {
        var storage = Criar();
        var armazenado = await storage.SalvarAsync(Conteudo("x"), "a.txt", "text/plain");

        await storage.ExcluirAsync(armazenado.CaminhoRelativo);

        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.AbrirAsync(armazenado.CaminhoRelativo));
    }

    public void Dispose()
    {
        if (Directory.Exists(raiz))
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
