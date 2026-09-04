namespace ErpFinanceiro.Domain;

/// <summary>
/// Tipos de documento anexável (seção 6 do escopo). Guardado como string no
/// banco (não o int do enum) para o valor continuar legível num dump e não
/// depender da ordem de declaração.
/// </summary>
public enum TipoDocumentoAnexo
{
    NotaFiscal,
    Boleto,
    Xml,
    Pdf,
    Comprovante,
    Contrato,
    Orcamento,
    Recibo,
    Observacao,
    Outros,
}
