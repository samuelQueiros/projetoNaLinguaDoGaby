using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.ContasAPagar;

public sealed record ResultadoContaPagar(ContaPagar? Conta, ResultadoOperacao Operacao)
{
    public static ResultadoContaPagar Ok(ContaPagar conta) => new(conta, ResultadoOperacao.Ok());

    public static ResultadoContaPagar Falha(params string[] erros) => new(null, ResultadoOperacao.Falha(erros));
}
