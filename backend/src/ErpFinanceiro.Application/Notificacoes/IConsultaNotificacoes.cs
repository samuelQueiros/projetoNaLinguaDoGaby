using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Notificacoes;

/// <summary>Notificação de vencimento apresentada no sino do sistema.</summary>
public sealed record NotificacaoVencimento(
    Guid Id,
    Guid FornecedorId,
    TipoNotificacao Tipo,
    string Titulo,
    string Fornecedor,
    DateOnly Vencimento);

/// <summary>Consulta os avisos que devem ser exibidos ao usuário autenticado.</summary>
public interface IConsultaNotificacoes
{
    /// <summary>
    /// Retorna contratos e contas a pagar cujo vencimento é amanhã. A janela
    /// é exata para que o aviso seja disparado com um dia de antecedência.
    /// </summary>
    Task<IReadOnlyList<NotificacaoVencimento>> ListarAsync(Guid usuarioId);

    /// <summary>Oculta para o usuário o alerta referente ao vencimento atual.</summary>
    Task<ResultadoOperacao> MarcarComoLidaAsync(Guid usuarioId, TipoNotificacao tipo, Guid referenciaId);
}
