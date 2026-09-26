namespace ErpFinanceiro.Application;

/// <summary>
/// Resultado de uma listagem paginada no banco (Skip/Take) — compartilhado
/// por qualquer módulo que precise paginar (Central de Documentos, Contas
/// a Pagar, etc.), em vez de devolver a lista inteira de uma vez.
/// </summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoPagina)
{
    public int TotalPaginas => TamanhoPagina <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanhoPagina));
}
