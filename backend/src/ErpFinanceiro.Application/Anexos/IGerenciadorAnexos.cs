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

public sealed record FiltroDocumentosContas(
    TipoDocumentoAnexo TipoDocumento,
    string? Busca = null,
    Guid? FornecedorId = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    int Pagina = 1,
    int TamanhoPagina = 20);

public sealed record DocumentoContaResumo(
    Guid Id,
    TipoDocumentoAnexo TipoDocumento,
    string NomeArquivo,
    long TamanhoBytes,
    DateTime CriadoEm,
    Guid ContaPagarId,
    string ContaDescricao,
    DateOnly ContaVencimento,
    Guid FornecedorId,
    string Fornecedor,
    string? NumeroNotaFiscal);

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

    Task<ResultadoPaginado<DocumentoContaResumo>> ListarDocumentosContasAsync(FiltroDocumentosContas filtro);

    Task<AnexoParaDownload?> BaixarAsync(Guid anexoId);

    Task<ResultadoOperacao> ExcluirAsync(Guid anexoId, Guid usuarioId);
}
