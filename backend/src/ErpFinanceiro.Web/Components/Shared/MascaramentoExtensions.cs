namespace ErpFinanceiro.Web.Components.Shared;

/// <summary>
/// Máscara de exibição pra número de conta/documento sensível — mostra só
/// os últimos 4 caracteres. Duplicado antes idênticamente em
/// ContasBancarias.razor e FornecedorDetalhe.razor (achado da auditoria de
/// qualidade).
/// </summary>
public static class MascaramentoExtensions
{
    public static string MascararFinal4(this string valor) =>
        valor.Length <= 4 ? new string('*', valor.Length) : new string('*', valor.Length - 4) + valor[^4..];

    public static string MascararFinal4OuTraco(this string? valor) =>
        string.IsNullOrEmpty(valor) ? "—" : valor.MascararFinal4();
}
