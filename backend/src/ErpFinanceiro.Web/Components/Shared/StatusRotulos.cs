using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Web.Components.Shared;

/// <summary>
/// Classe de <c>.pill</c> (ver app.css) para os status de conta a pagar —
/// mantém a mesma cor pro mesmo status em toda a tela (dashboard, listagem,
/// detalhe). O rótulo em português (<c>StatusAprovacao.Rotulo()</c> /
/// <c>StatusFinanceiro.Rotulo()</c>) vive em ErpFinanceiro.Domain, não
/// aqui — é usado também pela exportação Excel/CSV/PDF em Infrastructure,
/// que não pode depender de Web.
/// </summary>
public static class StatusRotulos
{
    public static string Pill(this StatusAprovacao status) => status switch
    {
        StatusAprovacao.Aprovada => "pill-sucesso",
        StatusAprovacao.Rejeitada => "pill-perigo",
        StatusAprovacao.AguardandoAprovacao => "pill-alerta",
        _ => "pill-neutra",
    };

    public static string Pill(this StatusFinanceiro status) => status switch
    {
        StatusFinanceiro.Paga => "pill-sucesso",
        StatusFinanceiro.Vencida or StatusFinanceiro.PagamentoRecusadoEstornado => "pill-perigo",
        StatusFinanceiro.AVencer or StatusFinanceiro.PagamentoNaoIdentificado => "pill-alerta",
        _ => "pill-neutra",
    };

    public static string Pill(this StatusCartao status) => status switch
    {
        StatusCartao.Ativo => "pill-sucesso",
        StatusCartao.Bloqueado => "pill-alerta",
        _ => "pill-neutra",
    };
}
