using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Anexos;

public sealed record NovoAnexo(
    EntidadeAnexo EntidadeTipo,
    Guid EntidadeId,
    TipoDocumentoAnexo TipoDocumento,
    Stream Conteudo,
    string NomeOriginal,
    string TipoConteudo);

public sealed record AnexoParaDownload(Stream Conteudo, string NomeArquivo, string TipoConteudo);

/// <summary>
/// Casos de uso de anexo (Passo 24) — compartilhado por ContaPagar,
/// Fornecedor, NotaFiscal, Boleto e CartaoDespesa. Grava/remove tanto a
/// linha em <see cref="Anexo"/> quanto o arquivo no storage, e audita a
/// operação (a timeline da conta mostra "NF anexada" etc.).
/// </summary>
public interface IGerenciadorAnexos
{
    Task<Anexo> AnexarAsync(NovoAnexo novo, Guid usuarioId);

    Task<IReadOnlyList<Anexo>> ListarAsync(EntidadeAnexo entidadeTipo, Guid entidadeId);

    Task<AnexoParaDownload?> BaixarAsync(Guid anexoId);

    Task<ResultadoOperacao> ExcluirAsync(Guid anexoId, Guid usuarioId);
}
