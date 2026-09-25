using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Relatorios;

/// <summary>
/// Exportação do relatório de contas a pagar (seção 10 do escopo) — Excel,
/// CSV e PDF, sempre com valor total + quantidade de registros.
/// </summary>
public interface IExportadorContasPagar
{
    byte[] ExportarExcel(IReadOnlyList<ContaPagar> contas);

    byte[] ExportarCsv(IReadOnlyList<ContaPagar> contas);

    byte[] ExportarPdf(IReadOnlyList<ContaPagar> contas);
}
