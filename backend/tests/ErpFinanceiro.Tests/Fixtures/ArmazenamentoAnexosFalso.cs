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

    /// <summary>
    /// Nomes de arquivo que devem simular a rejeição do storage real
    /// (extensão não permitida / tamanho excedido) — usado pra testar que
    /// um arquivo ruim no meio de um lote não derruba os demais.
    /// </summary>
    public HashSet<string> NomesQueDevemFalhar { get; } = new();

    public async Task<ArquivoArmazenado> SalvarAsync(Stream conteudo, string nomeOriginal, string tipoConteudo, CancellationToken ct = default)
    {
        if (NomesQueDevemFalhar.Contains(nomeOriginal))
        {
            throw new InvalidOperationException($"Extensão de arquivo não permitida: '{Path.GetExtension(nomeOriginal)}'.");
        }

        using var buffer = new MemoryStream();
        await conteudo.CopyToAsync(buffer, ct);
        var caminho = $"2026/01/{Guid.NewGuid():N}{Path.GetExtension(nomeOriginal)}";
        Arquivos[caminho] = buffer.ToArray();
        return new ArquivoArmazenado(caminho, buffer.Length, tipoConteudo);
    }

    public async Task<ArquivoArmazenado> SalvarEmAsync(string pastaRelativa, string nomeArquivo, Stream conteudo, string tipoConteudo, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await conteudo.CopyToAsync(buffer, ct);
        var caminho = $"{pastaRelativa}/{nomeArquivo}";
        Arquivos[caminho] = buffer.ToArray();
        return new ArquivoArmazenado(caminho, buffer.Length, tipoConteudo);
    }

    public Task<Stream> AbrirAsync(string caminhoRelativo, CancellationToken ct = default) =>
        Arquivos.TryGetValue(caminhoRelativo, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(caminhoRelativo);

    /// <summary>Simula falha de disco (arquivo em uso, permissão) no delete físico.</summary>
    public bool FalharAoExcluir { get; set; }

    public Task ExcluirAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        if (FalharAoExcluir)
        {
            throw new IOException("Simulação de falha de disco.");
        }

        Arquivos.Remove(caminhoRelativo);
        return Task.CompletedTask;
    }
}
