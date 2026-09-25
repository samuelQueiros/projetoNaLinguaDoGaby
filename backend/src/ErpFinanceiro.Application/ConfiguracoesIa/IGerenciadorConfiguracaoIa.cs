using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ConfiguracoesIa;

/// <summary>
/// Casos de uso de configuração de IA (seção "IA" do escopo). Provedor,
/// modelo, chave e timeout vêm de uma linha salva pela tela Configurações
/// de IA (Administrador) quando ela existe; sem isso, caem para as
/// variáveis de ambiente Ia__Documentos__*/Ia__Chat__* (bootstrap sem
/// precisar abrir a tela, ex. primeiro deploy). A leitura de documentos
/// (LeitorDocumentosHttp) e o agente de chat (IAgenteChatIa) consultam
/// <see cref="ObterAsync"/> a cada chamada, nunca em cache, pra trocar de
/// provedor/chave valer no próximo request.
/// </summary>
public interface IGerenciadorConfiguracaoIa
{
    Task<ConfiguracaoIa?> ObterAsync(FinalidadeConfiguracaoIa finalidade);

    /// <summary>
    /// Dados para a tela exibir/editar — nunca inclui a chave em texto
    /// claro, só se ela está definida (ver <see cref="ConfiguracaoIaResumo"/>).
    /// </summary>
    Task<ConfiguracaoIaResumo> ObterResumoAsync(FinalidadeConfiguracaoIa finalidade);

    /// <summary>
    /// Cria ou atualiza a linha salva pela tela para a finalidade
    /// (upsert por <see cref="FinalidadeConfiguracaoIa"/>).
    /// </summary>
    Task<ResultadoOperacao> SalvarAsync(FinalidadeConfiguracaoIa finalidade, SalvarConfiguracaoIaInput input, Guid usuarioId);

    /// <summary>
    /// Remove a linha salva pela tela — a finalidade volta a usar só a
    /// variável de ambiente (ou "não configurado", se ela também faltar).
    /// Idempotente: sem linha salva, não faz nada.
    /// </summary>
    Task RestaurarPadraoAsync(FinalidadeConfiguracaoIa finalidade, Guid usuarioId);

    /// <summary>
    /// Verificação simples de que a configuração atual consegue falar com
    /// o provedor — pra Documentos, checa o /health do serviço Python;
    /// pra Chat, faz uma chamada mínima ao provedor configurado.
    /// </summary>
    Task<ResultadoOperacao> TestarConexaoAsync(FinalidadeConfiguracaoIa finalidade);
}
