using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.DocumentosIa;

/// <summary>Um arquivo enviado pela Central de Documentos.</summary>
public sealed record ArquivoEnviado(Stream Conteudo, string NomeArquivo, string TipoConteudo);

/// <summary>
/// Dados que o Administrador confirma/corrige na revisão para o documento
/// virar uma <see cref="ContaPagar"/>. Espelha
/// <see cref="ContasAPagar.ContaPagarInput"/> — a IA preenche o rascunho,
/// o humano fecha.
/// </summary>
public sealed record RevisaoDocumentoInput(
    Guid FornecedorId,
    string Descricao,
    Guid? CategoriaId,
    Guid? CentroCustoId,
    DateOnly Vencimento,
    decimal ValorOriginal,
    decimal Desconto,
    decimal Juros,
    decimal Multa,
    Guid? FormaPagamentoId);

public sealed record FiltroDocumentosImportados(
    StatusImportacaoDocumento? Status = null,
    Guid? EnviadoPorId = null);
