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

/// <summary>
/// Filtro da Central de Documentos. <see cref="Busca"/> procura em nome do
/// arquivo, resumo e texto extraído (ILIKE — não é busca full-text, mas
/// cobre nome/fornecedor/CNPJ/conteúdo sem precisar indexar o jsonb de
/// <see cref="DocumentoImportado.Campos"/>, que não é query-able pelo EF).
/// <see cref="Pagina"/>/<see cref="TamanhoPagina"/> só valem para
/// <see cref="IGerenciadorDocumentosImportados.ListarPaginadoAsync"/> — o
/// <see cref="IGerenciadorDocumentosImportados.ListarAsync"/> "clássico"
/// (usado pelo dashboard e pelo agente de chat) continua devolvendo tudo
/// que bate no filtro, sem paginar.
/// </summary>
public sealed record FiltroDocumentosImportados(
    StatusImportacaoDocumento? Status = null,
    Guid? EnviadoPorId = null,
    string? Busca = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    TipoDocumentoDetectado? TipoDetectado = null,
    /// <summary>true = só Status=Falha; false = exclui Falha; null = não filtra.</summary>
    bool? ComErro = null,
    int Pagina = 1,
    int TamanhoPagina = 20);

/// <summary>
/// Contagens agregadas no banco (não sobre a página atual) para os
/// indicadores no topo da Central de Documentos.
/// </summary>
public sealed record IndicadoresDocumentosImportados(
    int Total,
    int AguardandoRevisao,
    int ComErro,
    int EmProcessamento);

public sealed record FalhaEnvioDocumento(string NomeArquivo, string Motivo);

public sealed record ResultadoEnvioDocumentos(
    IReadOnlyList<DocumentoImportado> Criados,
    IReadOnlyList<FalhaEnvioDocumento> Falhas);
