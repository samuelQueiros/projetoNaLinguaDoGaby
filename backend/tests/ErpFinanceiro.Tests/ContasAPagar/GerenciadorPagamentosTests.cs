using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.ContasAPagar;

public class GerenciadorPagamentosTests
{
    private sealed class RelogioFixo(DateOnly hoje) : IRelogio
    {
        public DateOnly Hoje() => hoje;
    }

    private static UserManager<Usuario> CriarUserManager(AppDbContext db)
    {
        var store = new UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>(db);
        return new UserManager<Usuario>(store, null!, new PasswordHasher<Usuario>(), [], [], null!, null!, null!,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<Usuario>>());
    }

    private static async Task<Usuario> CriarUsuarioComPapelAsync(AppDbContext db, UserManager<Usuario> userManager, string papel, string nome)
    {
        if (!db.Roles.Any(r => r.Name == papel))
        {
            db.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = papel, NormalizedName = papel });
            await db.SaveChangesAsync();
        }

        var usuario = new Usuario { Id = Guid.NewGuid(), UserName = $"{papel}@teste.local", Email = $"{papel}@teste.local", Nome = nome };
        await userManager.CreateAsync(usuario);
        await userManager.AddToRoleAsync(usuario, papel);
        return usuario;
    }

    private static async Task<(AppDbContext Db, GerenciadorPagamentos Gerenciador, RegistradorAuditoriaFalso Auditoria, UserManager<Usuario> UserManager, ContaPagar Conta, Guid ContaBancariaId, Guid CartaoId, Guid UsuarioId)>
        PrepararAsync(decimal valorFinal = 100m, DateOnly? hoje = null)
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = CriarUserManager(db);
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Teste", CnpjCpf = "12345678000199" };
        var contaBancaria = new ContaBancariaEmpresa { Id = Guid.NewGuid(), Banco = "Banco X", Agencia = "0001", Conta = "123", Apelido = "Principal" };
        var usuario = await CriarUsuarioComPapelAsync(db, userManager, "Financeiro", "Usuário Financeiro");
        var cartao = new Cartao
        {
            Id = Guid.NewGuid(), InstituicaoFinanceira = "Banco X", Bandeira = "Visa", Apelido = "Corp",
            UltimosQuatroDigitos = "1234", DiaFechamento = 5, DiaVencimento = 15, ResponsavelId = usuario.Id,
        };
        db.Fornecedores.Add(fornecedor);
        db.ContasBancariasEmpresa.Add(contaBancaria);
        db.Cartoes.Add(cartao);

        var conta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedor.Id,
            Descricao = "Conta teste",
            Vencimento = new DateOnly(2026, 12, 1),
            ValorOriginal = valorFinal,
            ValorFinal = valorFinal,
            StatusAprovacao = StatusAprovacao.Aprovada,
            StatusFinanceiro = StatusFinanceiro.EmAberto,
            CriadoPorId = usuario.Id,
        };
        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync();

        var relogio = new RelogioFixo(hoje ?? new DateOnly(2026, 6, 1));
        var auditoria = new RegistradorAuditoriaFalso();
        var gerenciador = new GerenciadorPagamentos(db, auditoria, relogio, userManager);
        return (db, gerenciador, auditoria, userManager, conta, contaBancaria.Id, cartao.Id, usuario.Id);
    }

    private static RegistrarPagamentoInput InputContaBancaria(decimal valor, Guid contaBancariaId, DateOnly? data = null) =>
        new(data ?? new DateOnly(2026, 6, 1), valor, null, contaBancariaId, null);

    [Fact]
    public async Task RegistrarAsync_pagamento_total_marca_conta_como_paga()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Paga, doBanco!.StatusFinanceiro);
    }

    [Fact]
    public async Task RegistrarAsync_pagamento_parcial_nao_marca_como_paga()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(40m, contaBancariaId), usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.NotEqual(StatusFinanceiro.Paga, doBanco!.StatusFinanceiro);
    }

    [Fact]
    public async Task RegistrarAsync_dois_pagamentos_parciais_somando_o_total_marca_como_paga()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);

        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(60m, contaBancariaId), usuarioId);
        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(40m, contaBancariaId), usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Paga, doBanco!.StatusFinanceiro);
    }

    [Fact]
    public async Task RegistrarAsync_com_valor_que_excede_o_valor_final_e_bloqueado()
    {
        var (_, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(60m, contaBancariaId), usuarioId);

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(60m, contaBancariaId), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAsync_com_conta_bancaria_e_cartao_ao_mesmo_tempo_e_rejeitado()
    {
        var (_, gerenciador, _, _, conta, contaBancariaId, cartaoId, usuarioId) = await PrepararAsync(100m);
        var input = new RegistrarPagamentoInput(new DateOnly(2026, 6, 1), 50m, null, contaBancariaId, cartaoId);

        var resultado = await gerenciador.RegistrarAsync(conta.Id, input, usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAsync_sem_conta_bancaria_nem_cartao_e_rejeitado()
    {
        var (_, gerenciador, _, _, conta, _, _, usuarioId) = await PrepararAsync(100m);
        var input = new RegistrarPagamentoInput(new DateOnly(2026, 6, 1), 50m, null, null, null);

        var resultado = await gerenciador.RegistrarAsync(conta.Id, input, usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAsync_em_conta_nao_aprovada_e_bloqueado()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        conta.StatusAprovacao = StatusAprovacao.Cadastrada;
        await db.SaveChangesAsync();

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(50m, contaBancariaId), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task EstornarAsync_reverte_status_de_paga_para_em_aberto()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m, hoje: new DateOnly(2026, 6, 1));
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);
        var pagamento = await db.Pagamentos.FirstAsync(p => p.ContaPagarId == conta.Id);

        var resultado = await gerenciador.EstornarAsync(pagamento.Id, "Pagamento em duplicidade", usuarioId);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.NotEqual(StatusFinanceiro.Paga, doBanco!.StatusFinanceiro);
        var pagamentoDoBanco = await db.Pagamentos.FindAsync(pagamento.Id);
        Assert.Equal(StatusPagamento.Estornado, pagamentoDoBanco!.Status);
    }

    [Fact]
    public async Task EstornarAsync_sem_motivo_e_bloqueado()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);
        var pagamento = await db.Pagamentos.FirstAsync(p => p.ContaPagarId == conta.Id);

        var resultado = await gerenciador.EstornarAsync(pagamento.Id, "", usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task EstornarAsync_pagamento_ja_estornado_e_bloqueado()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);
        var pagamento = await db.Pagamentos.FirstAsync(p => p.ContaPagarId == conta.Id);
        await gerenciador.EstornarAsync(pagamento.Id, "Motivo 1", usuarioId);

        var resultado = await gerenciador.EstornarAsync(pagamento.Id, "Motivo 2", usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAsync_por_usuario_sem_permissao_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, conta, contaBancariaId, _, _) = await PrepararAsync(100m);
        var consulta = await CriarUsuarioComPapelAsync(db, userManager, "Consulta", "Usuária Consulta");

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(50m, contaBancariaId), consulta.Id);

        Assert.False(resultado.Sucesso);
        Assert.Empty(db.Pagamentos.Where(p => p.ContaPagarId == conta.Id));
    }

    [Fact]
    public async Task EstornarAsync_por_usuario_sem_permissao_e_bloqueado()
    {
        var (db, gerenciador, _, userManager, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);
        var pagamento = await db.Pagamentos.FirstAsync(p => p.ContaPagarId == conta.Id);
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor", "Usuário Gestor");

        // Gestor aprova, mas não é Financeiro/Administrador — não deveria estornar.
        var resultado = await gerenciador.EstornarAsync(pagamento.Id, "Tentativa indevida", gestor.Id);

        Assert.False(resultado.Sucesso);
        var pagamentoDoBanco = await db.Pagamentos.FindAsync(pagamento.Id);
        Assert.Equal(StatusPagamento.Confirmado, pagamentoDoBanco!.Status);
    }

    [Fact]
    public async Task RegistrarAsync_por_administrador_e_permitido()
    {
        var (db, gerenciador, _, userManager, conta, contaBancariaId, _, _) = await PrepararAsync(100m);
        var admin = await CriarUsuarioComPapelAsync(db, userManager, "Administrador", "Usuário Admin");

        var resultado = await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(50m, contaBancariaId), admin.Id);

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAsync_conta_vencida_fica_com_status_vencida_apos_pagamento_parcial()
    {
        var (db, gerenciador, _, _, conta, contaBancariaId, _, usuarioId) =
            await PrepararAsync(100m, hoje: new DateOnly(2027, 1, 1)); // depois do vencimento (2026-12-01)

        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(10m, contaBancariaId, new DateOnly(2027, 1, 1)), usuarioId);

        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Vencida, doBanco!.StatusFinanceiro);
    }

    [Fact]
    public async Task RegistrarAsync_grava_log_de_auditoria()
    {
        var (_, gerenciador, auditoria, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);

        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(50m, contaBancariaId), usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("RegistrarPagamento", chamada.Acao);
        Assert.Equal(conta.Id, chamada.EntidadeId);
        Assert.Equal(usuarioId, chamada.UsuarioId);
    }

    [Fact]
    public async Task EstornarAsync_grava_log_de_auditoria()
    {
        var (db, gerenciador, auditoria, _, conta, contaBancariaId, _, usuarioId) = await PrepararAsync(100m);
        await gerenciador.RegistrarAsync(conta.Id, InputContaBancaria(100m, contaBancariaId), usuarioId);
        var pagamento = await db.Pagamentos.FirstAsync(p => p.ContaPagarId == conta.Id);
        auditoria.Chamadas.Clear(); // ignora a chamada do RegistrarAsync

        await gerenciador.EstornarAsync(pagamento.Id, "Motivo do estorno", usuarioId);

        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("EstornarPagamento", chamada.Acao);
        Assert.Equal(conta.Id, chamada.EntidadeId);
    }
}
