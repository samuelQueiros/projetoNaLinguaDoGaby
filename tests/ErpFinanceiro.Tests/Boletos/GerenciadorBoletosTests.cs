using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Boletos;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;

namespace ErpFinanceiro.Tests.Boletos;

public class GerenciadorBoletosTests
{
    private static async Task<(GerenciadorBoletos Gerenciador, RegistradorAuditoriaFalso Auditoria, AppDbContext Db, Guid FornecedorId, Guid ContaPagarId, Guid UsuarioId)> CriarAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Boleto", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        var usuarioId = Guid.NewGuid();
        var conta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedor.Id,
            Descricao = "Conta para boleto",
            Vencimento = new DateOnly(2026, 4, 10),
            ValorOriginal = 500m,
            ValorFinal = 500m,
            CriadoPorId = usuarioId,
        };
        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync();
        var auditoria = new RegistradorAuditoriaFalso();
        return (new GerenciadorBoletos(db, auditoria), auditoria, db, fornecedor.Id, conta.Id, usuarioId);
    }

    private static BoletoInput Input(Guid fornecedorId, Guid contaPagarId, string numero = "1000", decimal valor = 500m, StatusBoleto status = StatusBoleto.EmAberto) =>
        new(fornecedorId, contaPagarId, numero, "34191.79001 01043.510047 91020.150008 1 92340000015000",
            "34191920340000150000790010104351050047910201500", valor, new DateOnly(2026, 4, 10), null, "Banco Exemplo", status, null);

    [Fact]
    public async Task CriarAsync_valido_grava_e_audita()
    {
        var (gerenciador, auditoria, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId), usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Criar", Assert.Single(auditoria.Chamadas).Acao);
    }

    [Fact]
    public async Task CriarAsync_com_fornecedor_inexistente_falha()
    {
        var (gerenciador, _, _, _, contaPagarId, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(Guid.NewGuid(), contaPagarId), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_conta_a_pagar_inexistente_falha()
    {
        var (gerenciador, _, _, fornecedorId, _, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(fornecedorId, Guid.NewGuid()), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_com_valor_negativo_falha()
    {
        var (gerenciador, _, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId, valor: -1m), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task CriarAsync_sem_linha_digitavel_falha()
    {
        var (gerenciador, _, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();
        var input = new BoletoInput(fornecedorId, contaPagarId, "1000", "", null, 500m, new DateOnly(2026, 4, 10), null, null, StatusBoleto.EmAberto, null);

        var resultado = await gerenciador.CriarAsync(input, usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_fornecedor_e_status()
    {
        var (gerenciador, _, db, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();
        var outroFornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Outro", CnpjCpf = "99999999000188" };
        db.Fornecedores.Add(outroFornecedor);
        var outraConta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = outroFornecedor.Id,
            Descricao = "Outra conta",
            Vencimento = new DateOnly(2026, 5, 1),
            ValorOriginal = 100m,
            ValorFinal = 100m,
            CriadoPorId = usuarioId,
        };
        db.ContasPagar.Add(outraConta);
        await db.SaveChangesAsync();

        await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId, "111", status: StatusBoleto.EmAberto), usuarioId);
        await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId, "222", status: StatusBoleto.Pago), usuarioId);
        await gerenciador.CriarAsync(Input(outroFornecedor.Id, outraConta.Id, "333", status: StatusBoleto.EmAberto), usuarioId);

        Assert.Equal(2, (await gerenciador.ListarAsync(new FiltroBoletos(FornecedorId: fornecedorId))).Count);
        Assert.Single(await gerenciador.ListarAsync(new FiltroBoletos(FornecedorId: fornecedorId, Status: StatusBoleto.Pago)));
        Assert.Equal(2, (await gerenciador.ListarAsync(new FiltroBoletos(Status: StatusBoleto.EmAberto))).Count);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_periodo()
    {
        var (gerenciador, _, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();
        await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId, "1"), usuarioId);

        Assert.Single(await gerenciador.ListarAsync(new FiltroBoletos(Ano: 2026, Mes: 4)));
        Assert.Empty(await gerenciador.ListarAsync(new FiltroBoletos(Mes: 7)));
    }

    [Fact]
    public async Task EditarAsync_atualiza_e_audita()
    {
        var (gerenciador, auditoria, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();
        var criado = await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId), usuarioId);
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.EditarAsync(criado.Entidade!.Id, Input(fornecedorId, contaPagarId, status: StatusBoleto.Pago), usuarioId);

        Assert.True(resultado.Sucesso);
        var atualizado = await gerenciador.ObterAsync(criado.Entidade.Id);
        Assert.Equal(StatusBoleto.Pago, atualizado!.Status);
        Assert.Equal("Editar", Assert.Single(auditoria.Chamadas).Acao);
    }

    [Fact]
    public async Task ExcluirAsync_remove_e_audita()
    {
        var (gerenciador, auditoria, _, fornecedorId, contaPagarId, usuarioId) = await CriarAsync();
        var criado = await gerenciador.CriarAsync(Input(fornecedorId, contaPagarId), usuarioId);
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.ExcluirAsync(criado.Entidade!.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Excluir", Assert.Single(auditoria.Chamadas).Acao);
    }
}
