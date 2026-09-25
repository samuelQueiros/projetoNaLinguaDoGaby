using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Auditoria;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Auditoria;

public class RegistradorAuditoriaTests
{
    private sealed class HttpContextAccessorFalso : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Fact]
    public async Task RegistrarAsync_persiste_log_com_valores_serializados()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var registrador = new RegistradorAuditoria(db, new HttpContextAccessorFalso());
        var usuarioId = Guid.NewGuid();
        var entidadeId = Guid.NewGuid();

        await registrador.RegistrarAsync(
            usuarioId, "Editar", "ContaPagar", entidadeId,
            new { Status = "EmAberto" }, new { Status = "Paga" });

        var log = await db.LogsAuditoria.SingleAsync();
        Assert.Equal(usuarioId, log.UsuarioId);
        Assert.Equal("Editar", log.Acao);
        Assert.Equal("ContaPagar", log.TipoEntidade);
        Assert.Equal(entidadeId, log.EntidadeId);
        Assert.Contains("EmAberto", log.ValorAnteriorJson);
        Assert.Contains("Paga", log.ValorNovoJson);
    }

    [Fact]
    public async Task RegistrarAsync_com_valores_nulos_nao_grava_json()
    {
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var registrador = new RegistradorAuditoria(db, new HttpContextAccessorFalso());

        await registrador.RegistrarAsync(Guid.NewGuid(), "Excluir", "Fornecedor", Guid.NewGuid(), null, null);

        var log = await db.LogsAuditoria.SingleAsync();
        Assert.Null(log.ValorAnteriorJson);
        Assert.Null(log.ValorNovoJson);
    }

    [Fact]
    public async Task RegistrarAsync_com_entidade_de_dominio_crua_lanca_excecao()
    {
        // Blindagem estrutural (não só convenção/comentário) — recomendação
        // do security-auditor no Passo 21: nunca auditar uma entidade EF
        // crua, pelo risco de relationship fixup vazar dados sensíveis via
        // navegação (ex.: ContaPagar.Fornecedor.DadosBancarios).
        await using var db = AppDbContextFactory.CriarEmMemoria();
        var registrador = new RegistradorAuditoria(db, new HttpContextAccessorFalso());
        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "X", CnpjCpf = "12345678000199" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registrador.RegistrarAsync(Guid.NewGuid(), "Criar", "Fornecedor", fornecedor.Id, null, fornecedor));
    }
}
