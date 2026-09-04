namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// Fluxo de aprovação de ContaPagar (Passo 18 do plano do MVP, seção 16 do
/// escopo). Só Gestor/Administrador podem aprovar/rejeitar (seção 15 do
/// escopo) — checado aqui, na camada Application, não só na UI.
///
/// Importante (security-auditor, Passo 18): <c>usuarioId</c> deve sempre
/// vir do usuário autenticado da requisição atual (claims/AuthenticationState),
/// nunca de um campo de formulário ou query string — quem chama esta
/// interface é responsável por essa garantia.
/// </summary>
public interface IFluxoAprovacao
{
    Task<ResultadoOperacao> AprovarAsync(Guid contaPagarId, Guid usuarioId);

    /// <summary>Motivo é obrigatório ao rejeitar.</summary>
    Task<ResultadoOperacao> RejeitarAsync(Guid contaPagarId, Guid usuarioId, string motivo);
}
