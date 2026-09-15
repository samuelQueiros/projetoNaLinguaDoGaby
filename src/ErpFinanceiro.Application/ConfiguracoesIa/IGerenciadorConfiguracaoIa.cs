using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ConfiguracoesIa;

/// <summary>
/// Dados enviados pelo Administrador ao salvar a configuração de uma
/// finalidade (Documentos ou Chat). <see cref="NovaApiKey"/> é null quando
/// o admin não quer trocar a chave já cadastrada — nesse caso o valor
/// cifrado existente é mantido como está, nunca decifrado/reexibido.
/// </summary>
public sealed record ConfiguracaoIaInput(
    ProvedorIa Provedor,
    string Modelo,
    string? NovaApiKey,
    bool Ativo,
    int TimeoutSegundos);

/// <summary>
/// Casos de uso da tela de configuração de IA (seção "IA" do escopo) —
/// só Administrador pode ler/alterar. A leitura de documentos
/// (LeitorDocumentosHttp) e o agente de chat (IAgenteChatIa) consultam
/// isso a cada chamada, nunca em cache, pra trocar de provedor/chave
/// valer imediatamente sem reiniciar nada.
/// </summary>
public interface IGerenciadorConfiguracaoIa
{
    Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade);

    Task<ResultadoOperacao> SalvarAsync(FinalidadeConfiguracaoIa finalidade, ConfiguracaoIaInput input, Guid usuarioId);

    /// <summary>
    /// Verificação simples de que a configuração atual consegue falar com
    /// o provedor — pra Documentos, checa o /health do serviço Python;
    /// pra Chat, faz uma chamada mínima ao provedor configurado.
    /// </summary>
    Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade);
}
