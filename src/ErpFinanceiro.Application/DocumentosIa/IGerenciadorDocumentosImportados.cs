using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.DocumentosIa;

/// <summary>
/// Casos de uso da Central de Documentos (seção 9 do escopo;
/// docs/modulo-ia-documentos.md). Fluxo da fatia fina:
/// <list type="number">
///   <item><see cref="EnviarAsync"/> — salva os arquivos no storage, cria
///   um <see cref="DocumentoImportado"/> por arquivo (Status=Recebido) e
///   enfileira o processamento.</item>
///   <item><see cref="ProcessarAsync"/> — chamado pelo worker da fila: roda
///   o <see cref="ILeitorDocumentos"/> e guarda o resultado; o status vai
///   para AguardandoRevisao (ou Falha).</item>
///   <item><see cref="AprovarAsync"/> — só perfil Administrador; cria a
///   <see cref="ContaPagar"/> a partir dos dados revisados e anexa o
///   arquivo original a ela.</item>
///   <item><see cref="RejeitarAsync"/> — só perfil Administrador; descarta
///   o documento sem gerar lançamento.</item>
/// </list>
/// </summary>
public interface IGerenciadorDocumentosImportados
{
    /// <summary>
    /// Processa cada arquivo do lote de forma independente — um arquivo
    /// rejeitado (extensão não permitida, tamanho excedido) não aborta o
    /// resto do lote nem desfaz o que já foi salvo; ele só aparece em
    /// <see cref="ResultadoEnvioDocumentos.Falhas"/>. Antes, uma falha no
    /// arquivo N de um envio múltiplo abortava com exceção, deixando os
    /// arquivos 1..N-1 já salvos no banco/disco mas o chamador reportando
    /// como se nada tivesse sido enviado (achado da auditoria de
    /// qualidade).
    /// </summary>
    Task<ResultadoEnvioDocumentos> EnviarAsync(IReadOnlyList<ArquivoEnviado> arquivos, Guid usuarioId);

    Task ProcessarAsync(Guid documentoImportadoId, CancellationToken ct = default);

    /// <summary>Recoloca um documento que falhou na fila de leitura.</summary>
    Task<ResultadoOperacao> ReprocessarAsync(Guid id, Guid usuarioId);

    Task<DocumentoImportado?> ObterAsync(Guid id);

    Task<IReadOnlyList<DocumentoImportado>> ListarAsync(FiltroDocumentosImportados filtro);

    Task<ResultadoContaPagar> AprovarAsync(Guid id, RevisaoDocumentoInput dados, Guid usuarioId);

    Task<ResultadoOperacao> RejeitarAsync(Guid id, string motivo, Guid usuarioId);
}
