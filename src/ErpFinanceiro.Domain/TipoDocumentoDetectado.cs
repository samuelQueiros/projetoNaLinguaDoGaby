namespace ErpFinanceiro.Domain;

/// <summary>
/// Classificação que o serviço de IA atribui ao documento lido (seção 9 do
/// escopo). Diferente de <see cref="TipoDocumentoAnexo"/>, que é a
/// categoria escolhida por um humano ao anexar um arquivo manualmente.
/// </summary>
public enum TipoDocumentoDetectado
{
    NaoIdentificado,
    ComprovantePagamento,
    Contrato,
    NotaFiscal,
}
