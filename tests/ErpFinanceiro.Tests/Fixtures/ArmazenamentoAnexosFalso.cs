using ErpFinanceiro.Application.Anexos;

namespace ErpFinanceiro.Tests.Fixtures;

/// <summary>
/// Storage de anexos em memória para testes de GerenciadorAnexos — sem
/// tocar o disco. As blindagens do storage real (path traversal, allowlist,
/// tamanho) têm cobertura própria em ArmazenamentoAnexosDiscoTests.
/// </summary>
public sealed class ArmazenamentoAnexosFalso : IArmazenamentoAnexos
{
    public Dictionary<string, byte[]> Arquivos { get; } = new();

    public async Task<ArquivoArmazenado> SalvarAsync(Stream conteudo, string nomeOriginal, string tipoConteudo, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await conteudo.CopyToAsync(buffer, ct);
        var caminho = $"2026/01/{Guid.NewGuid():N}{Path.GetExtension(nomeOriginal)}";
        Arquivos[caminho] = buffer.ToArray();
        return new ArquivoArmazenado(caminho, buffer.Length, tipoConteudo);
    }

    public Task<Stream> AbrirAsync(string caminhoRelativo, CancellationToken ct = default) =>
        Arquivos.TryGetValue(caminhoRelativo, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(caminhoRelativo);

    public Task ExcluirAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        Arquivos.Remove(caminhoRelativo);
        return Task.CompletedTask;
    }
}
