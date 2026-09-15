namespace ErpFinanceiro.Domain;

/// <summary>
/// Pra qual finalidade uma <see cref="ConfiguracaoIa"/> vale — leitura de
/// documentos (Central de Documentos) e chat são workloads diferentes
/// (extração estruturada vs. conversa com tool-calling), então cada uma
/// tem seu próprio provedor/modelo/chave, mesmo que hoje apontem pro
/// mesmo Gemini.
/// </summary>
public enum FinalidadeConfiguracaoIa
{
    Documentos,
    Chat,
}

/// <summary>
/// Provedor de LLM. Espelha os provedores já suportados pelo serviço
/// Python (servico-documentos/app/extracao/llm_extractor.py) e pelo
/// agente de chat — trocar aqui não implica reescrever o catálogo de
/// ferramentas do chat nem o pipeline de extração, só o adapter do
/// provedor.
/// </summary>
public enum ProvedorIa
{
    Gemini,
    OpenAi,
    Anthropic,
}

/// <summary>
/// Configuração de IA editável pelo Administrador (seção "IA" do escopo).
/// Uma linha por <see cref="FinalidadeConfiguracaoIa"/>. A chave de API
/// nunca é guardada em texto puro — <see cref="ApiKey"/> é cifrada
/// em repouso (AES-256-GCM, mesmo mecanismo já usado para ChavePix de
/// fornecedor) e nunca deve ser exposta de volta, nem em auditoria.
/// </summary>
public class ConfiguracaoIa : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public FinalidadeConfiguracaoIa Finalidade { get; set; }

    public bool Ativo { get; set; }

    public ProvedorIa Provedor { get; set; }

    public string Modelo { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int TimeoutSegundos { get; set; } = 90;

    public Guid? AtualizadoPorId { get; set; }

    public Usuario? AtualizadoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
