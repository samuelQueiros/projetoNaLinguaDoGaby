using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ConfiguracoesIa;

/// <summary>
/// Casos de uso de configuração de IA (seção "IA" do escopo). Provedor,
/// modelo, chave e timeout vêm de variáveis de ambiente — não há mais
/// cadastro pela tela; a tela de Configurações de IA (Administrador) só
/// exibe o que está configurado e testa a conexão. A leitura de documentos
/// (LeitorDocumentosHttp) e o agente de chat (IAgenteChatIa) consultam
/// <see cref="ObterAsync"/> a cada chamada, nunca em cache, pra trocar de
/// provedor/chave valer no próximo request (reiniciar o container com a
/// env var nova já é suficiente — nenhum redeploy de código).
/// </summary>
public interface IGerenciadorConfiguracaoIa
{
    Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade);

    /// <summary>
    /// Verificação simples de que a configuração atual consegue falar com
    /// o provedor — pra Documentos, checa o /health do serviço Python;
    /// pra Chat, faz uma chamada mínima ao provedor configurado.
    /// </summary>
    Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade);
}
