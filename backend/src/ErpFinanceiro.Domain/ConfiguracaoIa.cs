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
/// Configuração de IA de uma <see cref="FinalidadeConfiguracaoIa"/>, lida a
/// partir de variáveis de ambiente (seção "Ia:Documentos"/"Ia:Chat" —
/// ver <see cref="ErpFinanceiro.Infrastructure.ConfiguracoesIa.GerenciadorConfiguracaoIa"/>),
/// nunca do banco. Não é uma entidade EF: existe só em memória, montada a
/// cada leitura, pra trocar de provedor/modelo/chave bastar reiniciar o
/// container com outro valor de env var — sem migração, sem tela de
/// cadastro, sem chave em texto puro persistida em lugar nenhum do app.
/// </summary>
public sealed class ConfiguracaoIa
{
    public required bool Ativo { get; init; }

    public required ProvedorIa Provedor { get; init; }

    public required string Modelo { get; init; }

    public string? ApiKey { get; init; }

    public int TimeoutSegundos { get; init; } = 90;
}
