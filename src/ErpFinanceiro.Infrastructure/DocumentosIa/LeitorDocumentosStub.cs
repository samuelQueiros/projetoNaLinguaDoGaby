using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.DocumentosIa;

/// <summary>
/// Implementação padrão de <see cref="ILeitorDocumentos"/> enquanto o
/// serviço Python (docling + LLM) não existe (etapa 2 —
/// docs/modulo-ia-documentos.md).
///
/// Não extrai nada: devolve <see cref="TipoDocumentoDetectado.NaoIdentificado"/>
/// com confiança 0. O documento entra na fila de revisão do mesmo jeito e o
/// Administrador preenche a despesa manualmente — o caminho ponta-a-ponta
/// (upload → fila → aprovação → ContaPagar) já funciona, só sem a "mágica".
/// </summary>
public sealed class LeitorDocumentosStub(ILogger<LeitorDocumentosStub> logger) : ILeitorDocumentos
{
    public Task<ResultadoLeituraDocumento> LerAsync(
        Stream conteudo,
        string nomeArquivo,
        string tipoConteudo,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "LeitorDocumentosStub: documento '{Arquivo}' ({Tipo}) aceito sem extração — revisão manual.",
            nomeArquivo, tipoConteudo);

        var resultado = new ResultadoLeituraDocumento(
            TipoDocumentoDetectado.NaoIdentificado,
            ConfiancaGeral: 0m,
            Campos: Array.Empty<CampoLido>());

        return Task.FromResult(resultado);
    }
}
