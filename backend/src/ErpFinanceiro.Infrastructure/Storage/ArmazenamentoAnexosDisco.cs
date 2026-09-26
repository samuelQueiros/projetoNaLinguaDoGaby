using ErpFinanceiro.Application.Anexos;

namespace ErpFinanceiro.Infrastructure.Storage;

public sealed class OpcoesArmazenamentoAnexos
{
    /// <summary>Raiz física do storage (disco local em dev, ponto de montagem em prod).</summary>
    public string RaizFisica { get; set; } = "storage";

    public long TamanhoMaximoBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Extensões permitidas, com ponto e minúsculas (ex.: ".pdf").</summary>
    public HashSet<string> ExtensoesPermitidas { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".xml", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".zip",
    };
}

/// <summary>
/// Storage de anexos em disco local/volume (Passo 23). Blindagens
/// (security-auditor): nome físico gerado pelo servidor (Guid + subpasta
/// por data), nunca o nome do usuário no path; extensão validada por
/// allowlist; tamanho máximo; todo caminho é resolvido e checado contra a
/// raiz para recusar path traversal (<c>..</c>, caminho absoluto, symlink
/// para fora).
/// </summary>
public sealed class ArmazenamentoAnexosDisco(OpcoesArmazenamentoAnexos opcoes) : IArmazenamentoAnexos
{
    private readonly string raiz = Path.GetFullPath(opcoes.RaizFisica);

    public async Task<ArquivoArmazenado> SalvarAsync(Stream conteudo, string nomeOriginal, string tipoConteudo, CancellationToken ct = default)
    {
        var extensao = Path.GetExtension(nomeOriginal);
        if (string.IsNullOrWhiteSpace(extensao) || !opcoes.ExtensoesPermitidas.Contains(extensao))
        {
            throw new InvalidOperationException($"Extensão de arquivo não permitida: '{extensao}'.");
        }

        var subpasta = DateTime.UtcNow.ToString("yyyy/MM");
        var nomeFisico = $"{Guid.NewGuid():N}{extensao.ToLowerInvariant()}";
        var caminhoRelativo = $"{subpasta}/{nomeFisico}";
        var caminhoAbsoluto = ResolverDentroDaRaiz(caminhoRelativo);

        Directory.CreateDirectory(Path.GetDirectoryName(caminhoAbsoluto)!);

        long tamanho;
        await using (var destino = new FileStream(caminhoAbsoluto, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await conteudo.CopyToAsync(destino, ct);
            tamanho = destino.Length;
        }

        if (tamanho > opcoes.TamanhoMaximoBytes)
        {
            File.Delete(caminhoAbsoluto);
            throw new InvalidOperationException(
                $"Arquivo excede o tamanho máximo de {opcoes.TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }

        return new ArquivoArmazenado(caminhoRelativo, tamanho, tipoConteudo);
    }

    public async Task<ArquivoArmazenado> SalvarEmAsync(string pastaRelativa, string nomeArquivo, Stream conteudo, string tipoConteudo, CancellationToken ct = default)
    {
        var extensao = Path.GetExtension(nomeArquivo);
        if (string.IsNullOrWhiteSpace(extensao) || !opcoes.ExtensoesPermitidas.Contains(extensao))
        {
            throw new InvalidOperationException($"Extensão de arquivo não permitida: '{extensao}'.");
        }

        var caminhoRelativo = ProximoCaminhoLivre(pastaRelativa, nomeArquivo);
        var caminhoAbsoluto = ResolverDentroDaRaiz(caminhoRelativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminhoAbsoluto)!);

        long tamanho;
        await using (var destino = new FileStream(caminhoAbsoluto, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await conteudo.CopyToAsync(destino, ct);
            tamanho = destino.Length;
        }

        if (tamanho > opcoes.TamanhoMaximoBytes)
        {
            File.Delete(caminhoAbsoluto);
            throw new InvalidOperationException(
                $"Arquivo excede o tamanho máximo de {opcoes.TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }

        return new ArquivoArmazenado(caminhoRelativo, tamanho, tipoConteudo);
    }

    /// <summary>
    /// Como o nome físico já é gerado pelo chamador (nunca o nome do
    /// usuário), colisão real é rara — mas dois envios na mesma pasta
    /// podem coincidir por acaso (mesmo Guid nunca, mas o nome pode vir de
    /// outra fonte determinística no futuro); resolve incrementando um
    /// sufixo em vez de sobrescrever ou falhar.
    /// </summary>
    private string ProximoCaminhoLivre(string pastaRelativa, string nomeArquivo)
    {
        var candidato = $"{pastaRelativa}/{nomeArquivo}";
        if (!File.Exists(ResolverDentroDaRaiz(candidato)))
        {
            return candidato;
        }

        var baseNome = Path.GetFileNameWithoutExtension(nomeArquivo);
        var extensao = Path.GetExtension(nomeArquivo);
        for (var tentativa = 1; ; tentativa++)
        {
            candidato = $"{pastaRelativa}/{baseNome}-{tentativa}{extensao}";
            if (!File.Exists(ResolverDentroDaRaiz(candidato)))
            {
                return candidato;
            }
        }
    }

    public Task<Stream> AbrirAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        var caminhoAbsoluto = ResolverDentroDaRaiz(caminhoRelativo);
        if (!File.Exists(caminhoAbsoluto))
        {
            throw new FileNotFoundException("Anexo não encontrado no storage.", caminhoRelativo);
        }

        Stream stream = new FileStream(caminhoAbsoluto, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task ExcluirAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        var caminhoAbsoluto = ResolverDentroDaRaiz(caminhoRelativo);
        if (File.Exists(caminhoAbsoluto))
        {
            File.Delete(caminhoAbsoluto);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolve o caminho relativo contra a raiz e garante que o resultado
    /// continua sob a raiz — recusa <c>..</c>, caminho absoluto ou qualquer
    /// truque de path traversal.
    /// </summary>
    private string ResolverDentroDaRaiz(string caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo) || Path.IsPathRooted(caminhoRelativo))
        {
            throw new InvalidOperationException("Caminho de anexo inválido.");
        }

        var combinado = Path.GetFullPath(Path.Combine(raiz, caminhoRelativo));
        var raizComSeparador = raiz.EndsWith(Path.DirectorySeparatorChar) ? raiz : raiz + Path.DirectorySeparatorChar;
        if (!combinado.StartsWith(raizComSeparador, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Caminho de anexo fora da raiz do storage.");
        }

        return combinado;
    }
}
