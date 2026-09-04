namespace ErpFinanceiro.Application.Usuarios;

/// <summary>
/// Resultado padrão de um caso de uso de escrita: sucesso/falha + lista de
/// erros legíveis (mensagens do Identity ou de validação de negócio).
/// </summary>
public sealed record ResultadoOperacao(bool Sucesso, IReadOnlyList<string> Erros)
{
    public static ResultadoOperacao Ok() => new(true, Array.Empty<string>());

    public static ResultadoOperacao Falha(params string[] erros) => new(false, erros);
}
