namespace ErpFinanceiro.Domain;

/// <summary>
/// Situação de um boleto (seção 8 do escopo — o campo "Status" não tem
/// valores enumerados no escopo original; estes cobrem o ciclo de vida
/// descrito ali, no mesmo vocabulário de <see cref="StatusFinanceiro"/>).
/// </summary>
public enum StatusBoleto
{
    EmAberto,
    Pago,
    Vencido,
    Cancelado,
}
