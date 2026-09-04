using System.Text;
using ClosedXML.Excel;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Relatorios;

namespace ErpFinanceiro.Tests.Relatorios;

public class ExportadorContasPagarTests
{
    static ExportadorContasPagarTests()
    {
        // QuestPDF exige a licença configurada antes do primeiro GeneratePdf()
        // — em produção isso acontece no Program.cs, aqui precisa ser feito
        // manualmente porque os testes rodam num host separado.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static List<ContaPagar> Contas() =>
    [
        new ContaPagar
        {
            Id = Guid.NewGuid(),
            Fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Um", CnpjCpf = "12345678000199" },
            Descricao = "Aluguel de setembro",
            Vencimento = new DateOnly(2026, 9, 10),
            ValorOriginal = 1500m,
            ValorFinal = 1500m,
            StatusFinanceiro = StatusFinanceiro.EmAberto,
            StatusAprovacao = StatusAprovacao.Aprovada,
        },
        new ContaPagar
        {
            Id = Guid.NewGuid(),
            Fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Dois", CnpjCpf = "99999999000188" },
            Descricao = "Internet",
            Vencimento = new DateOnly(2026, 9, 15),
            ValorOriginal = 300m,
            ValorFinal = 300m,
            StatusFinanceiro = StatusFinanceiro.Paga,
            StatusAprovacao = StatusAprovacao.Aprovada,
        },
    ];

    [Fact]
    public void ExportarExcel_gera_planilha_com_linhas_e_total()
    {
        var exportador = new ExportadorContasPagar();

        var bytes = exportador.ExportarExcel(Contas());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var planilha = workbook.Worksheet(1);

        Assert.Equal("Fornecedor", planilha.Cell(1, 1).GetString());
        Assert.Equal("Fornecedor Um", planilha.Cell(2, 1).GetString());
        Assert.Equal("Fornecedor Dois", planilha.Cell(3, 1).GetString());
        Assert.Equal("Total", planilha.Cell(5, 6).GetString());
        Assert.Equal(1800m, planilha.Cell(5, 7).GetValue<decimal>());
    }

    [Fact]
    public void ExportarCsv_contem_cabecalho_e_linhas()
    {
        var exportador = new ExportadorContasPagar();

        var bytes = exportador.ExportarCsv(Contas());
        var texto = Encoding.UTF8.GetString(bytes);

        Assert.Contains("Fornecedor", texto);
        Assert.Contains("Fornecedor Um", texto);
        Assert.Contains("Aluguel de setembro", texto);
    }

    [Fact]
    public void ExportarPdf_gera_documento_valido()
    {
        var exportador = new ExportadorContasPagar();

        var bytes = exportador.ExportarPdf(Contas());

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void ExportarExcel_sem_contas_nao_falha()
    {
        var exportador = new ExportadorContasPagar();

        var bytes = exportador.ExportarExcel([]);

        Assert.True(bytes.Length > 0);
    }
}
