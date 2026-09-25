namespace ErpFinanceiro.Domain;

/// <summary>
/// Rótulo em português dos status de conta a pagar — fonte única usada
/// pela Infrastructure (exportação Excel/CSV/PDF) e pelo front-end React
/// via API. Antes duplicado entre camadas (um achado crítico da auditoria
/// de segurança/qualidade: o rótulo do relatório exportado podia divergir
/// silenciosamente do rótulo mostrado na tela sempre que só um dos dois
/// lugares fosse atualizado ao adicionar um valor novo ao enum) — vive em
/// Domain porque Infrastructure não pode depender de Web (direção de
/// dependência inversa).
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
