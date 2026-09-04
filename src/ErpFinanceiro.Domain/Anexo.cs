namespace ErpFinanceiro.Domain;

/// <summary>
/// Entidades que podem receber anexos (polimorfismo por
/// EntidadeTipo + EntidadeId — seção 6 do escopo/CLAUDE.md). String, não
/// enum de verdade na FK, porque não há FK real: a coluna aponta para
/// tabelas diferentes conforme o tipo.
/// </summary>
public enum EntidadeAnexo
{
    ContaPagar,
    Fornecedor,
    NotaFiscal,
    Boleto,
    CartaoDespesa,
}

/// <summary>
/// Anexo genérico e polimórfico (CLAUDE.md seção 5). O arquivo em si fica
/// no storage (disco/volume — <c>IArmazenamentoAnexos</c>); esta linha
/// guarda só os metadados e o caminho relativo. Nome físico gerado pelo
/// servidor (nunca o enviado pelo usuário) para evitar path traversal e
/// sobrescrita — ver Passo 23 do plano.
/// </summary>
public class Anexo : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public EntidadeAnexo EntidadeTipo { get; set; }

    public Guid EntidadeId { get; set; }

    public TipoDocumentoAnexo TipoDocumento { get; set; }

    /// <summary>Nome original do arquivo, como enviado — só para exibição/download.</summary>
    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Caminho relativo dentro do storage (gerado pelo servidor).</summary>
    public string CaminhoArmazenamento { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    public string TipoConteudo { get; set; } = string.Empty;

    public Guid EnviadoPorId { get; set; }

    public Usuario? EnviadoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
