namespace ErpFinanceiro.Application;

/// <summary>
/// Resultado de um caso de uso de criação: a entidade criada (ou null em
/// falha) + o <see cref="ResultadoOperacao"/> padrão. Versão genérica do
/// que ContaPagar já fazia com ResultadoContaPagar, reaproveitável pelos
/// módulos de NF, Boleto, CartaoDespesa.
/// </summary>
public sealed record ResultadoCriacao<T>(T? Entidade, ResultadoOperacao Operacao)
    where T : class
{
    public bool Sucesso => Operacao.Sucesso;

    public static ResultadoCriacao<T> Ok(T entidade) => new(entidade, ResultadoOperacao.Ok());

    public static ResultadoCriacao<T> Falha(params string[] erros) => new(null, ResultadoOperacao.Falha(erros));
}
