using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Boletos;

public sealed record BoletoInput(
    Guid FornecedorId,
    Guid ContaPagarId,
    string Numero,
    string LinhaDigitavel,
    string? CodigoBarras,
    decimal Valor,
    DateOnly Vencimento,
    DateOnly? DataPagamento,
    string? Banco,
    StatusBoleto Status,
    string? Observacoes);

/// <summary>Filtros de busca de boletos (seção 8 do escopo).</summary>
public sealed record FiltroBoletos(
    Guid? FornecedorId = null,
    Guid? ContaPagarId = null,
    StatusBoleto? Status = null,
    DateOnly? VencimentoInicial = null,
    DateOnly? VencimentoFinal = null,
    int? Mes = null,
    int? Ano = null);

/// <summary>
/// Casos de uso de Boleto (seção 8 do escopo/Passo 7 do plano do MVP).
/// Exclusão sempre lógica — mesma convenção de ContaPagar/Fornecedor.
/// </summary>
public interface IGerenciadorBoletos
{
    Task<ResultadoCriacao<Boleto>> CriarAsync(BoletoInput input, Guid usuarioId);

    Task<ResultadoOperacao> EditarAsync(Guid id, BoletoInput input, Guid usuarioId);

    Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId);

    Task<Boleto?> ObterAsync(Guid id);

    Task<IReadOnlyList<Boleto>> ListarAsync(FiltroBoletos filtro);
}
