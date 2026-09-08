using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.DocumentosIa;

/// <summary>Um campo lido pelo serviço de extração, com confiança de 0 a 1.</summary>
public sealed record CampoLido(string Nome, string? Valor, decimal Confianca);

/// <summary>
/// Resposta do serviço de leitura de documentos (<see cref="ILeitorDocumentos"/>).
/// É o contrato entre o backend .NET e o serviço Python (docling + LLM) —
/// ver docs/modulo-ia-documentos.md.
///
/// Enquanto o serviço real não existe, <c>LeitorDocumentosStub</c> devolve
/// um resultado vazio de baixa confiança (revisão 100% manual).
/// </summary>
public sealed record ResultadoLeituraDocumento(
    TipoDocumentoDetectado TipoDetectado,
    decimal ConfiancaGeral,
    IReadOnlyList<CampoLido> Campos,
    string? Erro = null)
{
    public bool Sucesso => Erro is null;

    public static ResultadoLeituraDocumento Falha(string erro) =>
        new(TipoDocumentoDetectado.NaoIdentificado, 0m, Array.Empty<CampoLido>(), erro);
}
