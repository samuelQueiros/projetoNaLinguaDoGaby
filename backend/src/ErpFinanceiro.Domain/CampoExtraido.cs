namespace ErpFinanceiro.Domain;

/// <summary>
/// Um campo extraído de um <see cref="DocumentoImportado"/> pela IA, com a
/// confiança individual da leitura (seção 9 do escopo: "nível de confiança
/// por campo" — ex.: valor 98%, nome do fornecedor 60% porque a letra
/// estava borrada). Sem ciclo de vida próprio fora do documento — mapeado
/// como coleção owned / jsonb.
/// </summary>
public class CampoExtraido
{
    /// <summary>
    /// Nome canônico do campo: <c>valor</c>, <c>data</c>, <c>vencimento</c>,
    /// <c>fornecedor</c>, <c>cnpj</c>, <c>numeroNota</c>,
    /// <c>formaPagamento</c>, <c>banco</c>, <c>vigenciaInicio</c>,
    /// <c>vigenciaFim</c>.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Valor lido, sempre como texto — a conversão/validação é na revisão.</summary>
    public string? Valor { get; set; }

    /// <summary>Confiança da leitura deste campo, de 0 a 1.</summary>
    public decimal Confianca { get; set; }
}
