namespace ErpFinanceiro.Application.ContasAPagar;

/// <summary>
/// Fluxo de aprovação de ContaPagar (Passo 18 do plano do MVP, seção 16 do
/// escopo). Só Gestor/Administrador podem aprovar/rejeitar (seção 15 do
/// escopo) — checado aqui, na camada Application, não só na UI.
/// </summary>
public interface IFluxoAprovacao
{
    Task<ResultadoOperacao> AprovarAsync(Guid contaPagarId, Guid usuarioId);

    /// <summary>Motivo é obrigatório ao rejeitar.</summary>
    Task<ResultadoOperacao> RejeitarAsync(Guid contaPagarId, Guid usuarioId, string motivo);
}
