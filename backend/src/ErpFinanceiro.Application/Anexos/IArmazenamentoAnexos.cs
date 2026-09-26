namespace ErpFinanceiro.Application.Anexos;

/// <summary>
/// Resultado de um upload: o caminho relativo gerado pelo servidor e os
/// metadados detectados/validados na gravação.
/// </summary>
public sealed record ArquivoArmazenado(string CaminhoRelativo, long TamanhoBytes, string TipoConteudo);

/// <summary>
/// Abstração de storage de anexos (CLAUDE.md seção 3: começa em disco
/// local/volume, troca futura para Blob/S3 sem tocar a regra de negócio).
/// A implementação é responsável por: gerar um nome físico não previsível
/// (nunca reaproveitar o nome do usuário no path), validar extensão e
/// tamanho máximo, e nunca deixar o caminho escapar da raiz configurada.
/// </summary>
public interface IArmazenamentoAnexos
{
    Task<ArquivoArmazenado> SalvarAsync(Stream conteudo, string nomeOriginal, string tipoConteudo, CancellationToken ct = default);

    /// <summary>
    /// Como <see cref="SalvarAsync"/>, mas grava numa pasta relativa
    /// explícita (não a pasta yyyy/MM automática) — usado quando a
    /// estrutura de pastas importa para navegação humana (ex.: contratos
    /// de fornecedor, organizados por fornecedor/ano). O nome físico do
    /// arquivo (<paramref name="nomeArquivo"/>) continua sendo gerado pelo
    /// chamador de forma não previsível — nunca o nome original do
    /// usuário — só a pasta muda; qualquer segmento de
    /// <paramref name="pastaRelativa"/> originado de texto livre do
    /// usuário deve ser sanitizado pelo chamador antes de chegar aqui.
    /// </summary>
    Task<ArquivoArmazenado> SalvarEmAsync(string pastaRelativa, string nomeArquivo, Stream conteudo, string tipoConteudo, CancellationToken ct = default);

    Task<Stream> AbrirAsync(string caminhoRelativo, CancellationToken ct = default);

    Task ExcluirAsync(string caminhoRelativo, CancellationToken ct = default);
}
