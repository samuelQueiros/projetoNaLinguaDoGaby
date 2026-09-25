namespace ErpFinanceiro.Domain;

/// <summary>
/// Um documento enviado pela Central de Documentos e lido pelo serviço de
/// IA (seção 9 do escopo; fluxo em docs/modulo-ia-documentos.md).
///
/// Nenhum lançamento entra como oficial direto da IA: este registro fica
/// em <see cref="StatusImportacaoDocumento.AguardandoRevisao"/> até um
/// Administrador aprovar, quando então vira uma <see cref="ContaPagar"/>
/// (referenciada em <see cref="ContaPagarId"/>).
///
/// O arquivo em si fica no storage (via <c>IArmazenamentoAnexos</c>, igual
/// a <see cref="Anexo"/>); esta linha guarda só metadados, o caminho
/// relativo e o resultado da extração.
/// </summary>
public class DocumentoImportado : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    /// <summary>Nome original do arquivo enviado — só para exibição.</summary>
    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Caminho relativo dentro do storage (gerado pelo servidor).</summary>
    public string CaminhoArmazenamento { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    public string TipoConteudo { get; set; } = string.Empty;

    public StatusImportacaoDocumento Status { get; set; } = StatusImportacaoDocumento.Recebido;

    public TipoDocumentoDetectado TipoDetectado { get; set; } = TipoDocumentoDetectado.NaoIdentificado;

    /// <summary>Confiança geral da leitura (0 a 1), reportada pelo serviço de IA.</summary>
    public decimal ConfiancaGeral { get; set; }

    /// <summary>Campos extraídos, cada um com a confiança individual da leitura.</summary>
    public List<CampoExtraido> Campos { get; set; } = new();

    /// <summary>
    /// Texto integral extraído do documento pelo próprio sistema (pypdf/OCR
    /// no serviço Python — nunca por LLM), truncado lá. Null quando ainda
    /// não processado ou quando o leitor em uso não faz extração real
    /// (<c>LeitorDocumentosStub</c>). Base para <see cref="Resumo"/> e para
    /// o agente de chat consultar o conteúdo do documento sob demanda.
    /// </summary>
    public string? TextoExtraido { get; set; }

    /// <summary>
    /// Resumo curto, gerado deterministicamente a partir dos campos
    /// extraídos (tipo, fornecedor, valor, datas) — não depende de LLM.
    /// Null enquanto não processado.
    /// </summary>
    public string? Resumo { get; set; }

    /// <summary>Observação de erro quando <see cref="Status"/> é <see cref="StatusImportacaoDocumento.Falha"/>.</summary>
    public string? MensagemErro { get; set; }

    /// <summary>Conta a pagar criada na aprovação (null enquanto não aprovado).</summary>
    public Guid? ContaPagarId { get; set; }

    public ContaPagar? ContaPagar { get; set; }

    /// <summary>Motivo informado pelo Administrador ao rejeitar.</summary>
    public string? MotivoRejeicao { get; set; }

    public Guid EnviadoPorId { get; set; }

    public Usuario? EnviadoPor { get; set; }

    /// <summary>Administrador que aprovou ou rejeitou (null enquanto pendente).</summary>
    public Guid? RevisadoPorId { get; set; }

    public Usuario? RevisadoPor { get; set; }

    public DateTime? ProcessadoEm { get; set; }

    public DateTime? RevisadoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
