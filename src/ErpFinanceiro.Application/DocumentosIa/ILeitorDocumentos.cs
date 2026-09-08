namespace ErpFinanceiro.Application.DocumentosIa;

/// <summary>
/// Abstração do serviço que lê um documento (PDF/imagem) e devolve os
/// dados estruturados. Duas implementações:
/// <list type="bullet">
///   <item><c>LeitorDocumentosStub</c> — padrão até o serviço Python subir;
///   devolve resultado vazio de confiança 0 (revisão manual).</item>
///   <item><c>LeitorDocumentosHttp</c> — chama o serviço Python
///   (docling + LLM) via HTTP.</item>
/// </list>
/// A troca é por configuração (<c>Ia:Leitor:Modo</c>), sem tocar nos casos
/// de uso.
/// </summary>
public interface ILeitorDocumentos
{
    Task<ResultadoLeituraDocumento> LerAsync(
        Stream conteudo,
        string nomeArquivo,
        string tipoConteudo,
        CancellationToken ct = default);
}
