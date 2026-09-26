namespace ErpFinanceiro.Domain;

/// <summary>
/// Contrato de um fornecedor (N por fornecedor) — nome livre, vigência
/// (início/fim) e um arquivo anexado. O arquivo em si fica no storage (via
/// IArmazenamentoAnexos, igual a <see cref="Anexo"/>/<see cref="DocumentoImportado"/>);
/// esta linha guarda só metadados e o caminho relativo.
/// </summary>
public class ContratoFornecedor : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public Guid FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public string Nome { get; set; } = string.Empty;

    public DateOnly VigenciaInicio { get; set; }

    public DateOnly VigenciaFim { get; set; }

    /// <summary>Nome original do arquivo enviado — só para exibição.</summary>
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
