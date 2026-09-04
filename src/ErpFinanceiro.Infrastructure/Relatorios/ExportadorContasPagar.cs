using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using ErpFinanceiro.Application.Relatorios;
using ErpFinanceiro.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpFinanceiro.Infrastructure.Relatorios;

public sealed class ExportadorContasPagar : IExportadorContasPagar
{
    private static readonly string[] Cabecalho =
    [
        "Fornecedor", "Descrição", "Categoria", "Centro de custo", "Vencimento",
        "Valor original", "Valor final", "Status financeiro", "Status aprovação",
    ];

    public byte[] ExportarExcel(IReadOnlyList<ContaPagar> contas)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Contas a pagar");

        for (var i = 0; i < Cabecalho.Length; i++)
        {
            planilha.Cell(1, i + 1).Value = Cabecalho[i];
            planilha.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var linha = 2;
        foreach (var c in contas)
        {
            planilha.Cell(linha, 1).Value = c.Fornecedor?.RazaoSocial ?? "";
            planilha.Cell(linha, 2).Value = c.Descricao;
            planilha.Cell(linha, 3).Value = c.Categoria?.Nome ?? "";
            planilha.Cell(linha, 4).Value = c.CentroCusto?.Nome ?? "";
            planilha.Cell(linha, 5).Value = c.Vencimento.ToDateTime(TimeOnly.MinValue);
            planilha.Cell(linha, 5).Style.DateFormat.Format = "dd/MM/yyyy";
            planilha.Cell(linha, 6).Value = c.ValorOriginal;
            planilha.Cell(linha, 7).Value = c.ValorFinal;
            planilha.Cell(linha, 8).Value = RotuloStatusFinanceiro(c.StatusFinanceiro);
            planilha.Cell(linha, 9).Value = RotuloStatusAprovacao(c.StatusAprovacao);
            linha++;
        }

        planilha.Cell(linha + 1, 6).Value = "Total";
        planilha.Cell(linha + 1, 6).Style.Font.Bold = true;
        planilha.Cell(linha + 1, 7).Value = contas.Sum(c => c.ValorFinal);
        planilha.Cell(linha + 1, 7).Style.Font.Bold = true;
        planilha.Cell(linha + 2, 6).Value = "Quantidade";
        planilha.Cell(linha + 2, 7).Value = contas.Count;

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportarCsv(IReadOnlyList<ContaPagar> contas)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.GetCultureInfo("pt-BR"))))
        {
            foreach (var titulo in Cabecalho)
            {
                csv.WriteField(titulo);
            }
            csv.NextRecord();

            foreach (var c in contas)
            {
                csv.WriteField(c.Fornecedor?.RazaoSocial ?? "");
                csv.WriteField(c.Descricao);
                csv.WriteField(c.Categoria?.Nome ?? "");
                csv.WriteField(c.CentroCusto?.Nome ?? "");
                csv.WriteField(c.Vencimento.ToString("dd/MM/yyyy"));
                csv.WriteField(c.ValorOriginal);
                csv.WriteField(c.ValorFinal);
                csv.WriteField(RotuloStatusFinanceiro(c.StatusFinanceiro));
                csv.WriteField(RotuloStatusAprovacao(c.StatusAprovacao));
                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }

    public byte[] ExportarPdf(IReadOnlyList<ContaPagar> contas)
    {
        var total = contas.Sum(c => c.ValorFinal);

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text("Relatório de contas a pagar").FontSize(16).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        foreach (var titulo in new[] { "Fornecedor", "Descrição", "Categoria", "Centro de custo", "Vencimento", "Valor final", "Status financeiro", "Status aprovação" })
                        {
                            header.Cell().Border(1).Padding(2).Text(titulo).Bold();
                        }
                    });

                    foreach (var c in contas)
                    {
                        table.Cell().Border(1).Padding(2).Text(c.Fornecedor?.RazaoSocial ?? "");
                        table.Cell().Border(1).Padding(2).Text(c.Descricao);
                        table.Cell().Border(1).Padding(2).Text(c.Categoria?.Nome ?? "");
                        table.Cell().Border(1).Padding(2).Text(c.CentroCusto?.Nome ?? "");
                        table.Cell().Border(1).Padding(2).Text(c.Vencimento.ToString("dd/MM/yyyy"));
                        table.Cell().Border(1).Padding(2).Text(c.ValorFinal.ToString("C", CultureInfo.GetCultureInfo("pt-BR")));
                        table.Cell().Border(1).Padding(2).Text(RotuloStatusFinanceiro(c.StatusFinanceiro));
                        table.Cell().Border(1).Padding(2).Text(RotuloStatusAprovacao(c.StatusAprovacao));
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().Text($"{contas.Count} conta(s) encontrada(s)");
                    col.Item().Text($"Total: {total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}").Bold();
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string RotuloStatusFinanceiro(StatusFinanceiro status) => status switch
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

    private static string RotuloStatusAprovacao(StatusAprovacao status) => status switch
    {
        StatusAprovacao.Cadastrada => "Cadastrada",
        StatusAprovacao.AguardandoAprovacao => "Aguardando aprovação",
        StatusAprovacao.Aprovada => "Aprovada",
        StatusAprovacao.Rejeitada => "Rejeitada",
        _ => status.ToString(),
    };
}
