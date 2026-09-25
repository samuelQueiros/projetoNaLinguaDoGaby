namespace ErpFinanceiro.Domain;

/// <summary>
/// Rótulo em português dos status de conta a pagar — fonte única usada
/// tanto pela Web (tela, pill colorido) quanto pela Infrastructure
/// (exportação Excel/CSV/PDF). Antes duplicado entre as duas camadas (um
/// achado crítico da auditoria de segurança/qualidade: o rótulo do
/// relatório exportado podia divergir silenciosamente do rótulo mostrado
/// na tela sempre que só um dos dois lugares fosse atualizado ao adicionar
/// um valor novo ao enum) — vive em Domain, não em Web, porque
/// Infrastructure não pode depender de Web (direção de dependência
/// inversa). A cor/classe CSS do pill continua em
/// ErpFinanceiro.Web.Components.Shared.StatusRotulos, que é mesmo uma
/// decisão de apresentação e não pertence aqui.
/// </summary>
public static class StatusRotulos
{
    public static string Rotulo(this StatusAprovacao status) => status switch
    {
        StatusAprovacao.Cadastrada => "Cadastrada",
        StatusAprovacao.AguardandoAprovacao => "Aguardando aprovação",
        StatusAprovacao.Aprovada => "Aprovada",
        StatusAprovacao.Rejeitada => "Rejeitada",
        _ => status.ToString(),
    };

    public static string Rotulo(this StatusFinanceiro status) => status switch
    {
        StatusFinanceiro.Agendada => "Agendada",
        StatusFinanceiro.EmAberto => "Em aberto",
        StatusFinanceiro.AVencer => "A vencer",
        StatusFinanceiro.Vencida => "Vencida",
        StatusFinanceiro.Paga => "Paga",
        StatusFinanceiro.Cancelada => "Cancelada",
        StatusFinanceiro.PagamentoNaoIdentificado => "Pagamento não identificado",
        StatusFinanceiro.PagamentoRecusadoEstornado => "Pagamento recusado/estornado",
        _ => status.ToString(),
    };

    public static string Rotulo(this StatusCartao status) => status switch
    {
        StatusCartao.Ativo => "Ativo",
        StatusCartao.Bloqueado => "Bloqueado",
        StatusCartao.Cancelado => "Cancelado",
        _ => status.ToString(),
    };
}
