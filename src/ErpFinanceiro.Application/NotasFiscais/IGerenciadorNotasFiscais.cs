using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.NotasFiscais;

public sealed record NotaFiscalInput(
    Guid FornecedorId,
    Guid? ContaPagarId,
    string Numero,
    string? Serie,
    DateOnly Emissao,
    decimal Valor,
    DateOnly? Vencimento,
    Guid? CategoriaId,
    Guid? CentroCustoId,
    string? Observacoes);

/// <summary>Filtros de busca de NF (seção 7 do escopo).</summary>
public sealed record FiltroNotasFiscais(
    string? Numero = null,
    Guid? FornecedorId = null,
    string? CnpjCpf = null,
    DateOnly? EmissaoInicial = null,
    DateOnly? EmissaoFinal = null,
    int? Mes = null,
    int? Ano = null,
    Guid? ContaPagarId = null);

public interface IGerenciadorNotasFiscais
{
    Task<ResultadoCriacao<NotaFiscal>> CriarAsync(NotaFiscalInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarAsync(Guid id, NotaFiscalInput input, Guid usuarioId);

    Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId);

    Task<NotaFiscal?> ObterAsync(Guid id);

    Task<IReadOnlyList<NotaFiscal>> ListarAsync(FiltroNotasFiscais filtro);
}
