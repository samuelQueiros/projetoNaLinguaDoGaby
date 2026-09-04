using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.ContasAPagar;

public class FluxoAprovacaoTests
{
    private sealed class AuditoriaFalsa : IRegistradorAuditoria
    {
        public Task RegistrarAsync(Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo) =>
            Task.CompletedTask;
    }

    private static UserManager<Usuario> CriarUserManager(AppDbContext db)
    {
        var store = new UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>(db);
        return new UserManager<Usuario>(store, null!, new PasswordHasher<Usuario>(), [], [], null!, null!, null!,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<Usuario>>());
    }

    private static async Task<Usuario> CriarUsuarioComPapelAsync(AppDbContext db, UserManager<Usuario> userManager, string papel)
    {
        // Sem ILookupNormalizer configurado neste UserManager de teste,
        // NormalizeName não altera o valor — NormalizedName precisa bater
        // exatamente com o nome usado em AddToRoleAsync/GetRolesAsync.
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = papel, NormalizedName = papel };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var usuario = new Usuario { Id = Guid.NewGuid(), UserName = $"{papel}@teste.local", Email = $"{papel}@teste.local", Nome = papel };
        await userManager.CreateAsync(usuario);
        await userManager.AddToRoleAsync(usuario, papel);
        return usuario;
    }

    private static async Task<(AppDbContext Db, FluxoAprovacao Fluxo, UserManager<Usuario> UserManager, ContaPagar Conta)> PrepararAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = CriarUserManager(db);
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Teste", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);

        var conta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedor.Id,
            Descricao = "Conta teste",
            Vencimento = new DateOnly(2026, 12, 1),
            ValorOriginal = 100m,
            ValorFinal = 100m,
            StatusAprovacao = StatusAprovacao.Cadastrada,
            StatusFinanceiro = StatusFinanceiro.EmAberto,
            CriadoPorId = Guid.NewGuid(),
        };
        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync();

        var fluxo = new FluxoAprovacao(db, userManager, new AuditoriaFalsa());
        return (db, fluxo, userManager, conta);
    }

    [Fact]
    public async Task AprovarAsync_por_gestor_muda_status_para_aprovada()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");

        var resultado = await fluxo.AprovarAsync(conta.Id, gestor.Id);

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Aprovada, doBanco!.StatusAprovacao);
        Assert.Single(db.AprovacoesConta.Where(a => a.ContaPagarId == conta.Id));
    }

    [Fact]
    public async Task AprovarAsync_por_usuario_sem_permissao_e_bloqueado()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var consulta = await CriarUsuarioComPapelAsync(db, userManager, "Consulta");

        var resultado = await fluxo.AprovarAsync(conta.Id, consulta.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Cadastrada, doBanco!.StatusAprovacao);
    }

    [Fact]
    public async Task AprovarAsync_por_financeiro_e_bloqueado()
    {
        // Financeiro cadastra/paga, mas não aprova (seção 15 do escopo:
        // aprovação é atribuição de Gestor/Administrador).
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var financeiro = await CriarUsuarioComPapelAsync(db, userManager, "Financeiro");

        var resultado = await fluxo.AprovarAsync(conta.Id, financeiro.Id);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RejeitarAsync_sem_motivo_retorna_falha()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");

        var resultado = await fluxo.RejeitarAsync(conta.Id, gestor.Id, "");

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Cadastrada, doBanco!.StatusAprovacao);
    }

    [Fact]
    public async Task RejeitarAsync_com_motivo_muda_status_e_grava_motivo_na_conta()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var administrador = await CriarUsuarioComPapelAsync(db, userManager, "Administrador");

        var resultado = await fluxo.RejeitarAsync(conta.Id, administrador.Id, "Fornecedor com pendência cadastral.");

        Assert.True(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Rejeitada, doBanco!.StatusAprovacao);
        Assert.Equal("Fornecedor com pendência cadastral.", doBanco.MotivoCancelamentoRejeicao);
    }

    [Fact]
    public async Task AprovarAsync_conta_ja_aprovada_nao_pode_ser_aprovada_de_novo()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");
        await fluxo.AprovarAsync(conta.Id, gestor.Id);

        var resultado = await fluxo.AprovarAsync(conta.Id, gestor.Id);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task AprovarAsync_conta_ja_aprovada_nao_pode_ser_rejeitada()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");
        await fluxo.AprovarAsync(conta.Id, gestor.Id);

        var resultado = await fluxo.RejeitarAsync(conta.Id, gestor.Id, "Mudei de ideia");

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Aprovada, doBanco!.StatusAprovacao);
    }

    [Fact]
    public async Task RejeitarAsync_conta_ja_rejeitada_nao_pode_ser_aprovada()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");
        await fluxo.RejeitarAsync(conta.Id, gestor.Id, "Documentação incompleta");

        var resultado = await fluxo.AprovarAsync(conta.Id, gestor.Id);

        Assert.False(resultado.Sucesso);
        var doBanco = await db.ContasPagar.FindAsync(conta.Id);
        Assert.Equal(StatusAprovacao.Rejeitada, doBanco!.StatusAprovacao);
    }

    [Fact]
    public async Task AprovarAsync_com_conta_inexistente_retorna_falha()
    {
        var (db, fluxo, userManager, _) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");

        var resultado = await fluxo.AprovarAsync(Guid.NewGuid(), gestor.Id);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task AprovarAsync_com_usuario_inexistente_retorna_falha()
    {
        var (_, fluxo, _, conta) = await PrepararAsync();

        var resultado = await fluxo.AprovarAsync(conta.Id, Guid.NewGuid());

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RejeitarAsync_com_motivo_somente_espacos_em_branco_retorna_falha()
    {
        var (db, fluxo, userManager, conta) = await PrepararAsync();
        var gestor = await CriarUsuarioComPapelAsync(db, userManager, "Gestor");

        var resultado = await fluxo.RejeitarAsync(conta.Id, gestor.Id, "   ");

        Assert.False(resultado.Sucesso);
    }
}
