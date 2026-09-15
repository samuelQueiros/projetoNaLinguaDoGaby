using System.Text;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Anexos;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErpFinanceiro.Tests.Anexos;

public class GerenciadorAnexosTests
{
    private static async Task<(GerenciadorAnexos Gerenciador, ArmazenamentoAnexosFalso Storage, RegistradorAuditoriaFalso Auditoria, AppDbContext Db, Guid UsuarioId)> CriarAsync()
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var usuario = new Usuario { Id = Guid.NewGuid(), Nome = "Enviador", UserName = "enviador@erp.local", Email = "enviador@erp.local" };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        var storage = new ArmazenamentoAnexosFalso();
        var auditoria = new RegistradorAuditoriaFalso();
        return (new GerenciadorAnexos(db, storage, auditoria, NullLogger<GerenciadorAnexos>.Instance), storage, auditoria, db, usuario.Id);
    }

    private static NovoAnexo Novo(Guid entidadeId, string nome = "nota.pdf", string texto = "conteudo") =>
        new(EntidadeAnexo.ContaPagar, entidadeId, TipoDocumentoAnexo.NotaFiscal,
            new MemoryStream(Encoding.UTF8.GetBytes(texto)), nome, "application/pdf");

    [Fact]
    public async Task AnexarAsync_grava_linha_metadados_e_audita()
    {
        var (gerenciador, storage, auditoria, _, usuarioId) = await CriarAsync();
        var contaId = Guid.NewGuid();

        var anexo = await gerenciador.AnexarAsync(Novo(contaId, "Nota Fiscal 55.pdf"), usuarioId);

        Assert.Equal("Nota Fiscal 55.pdf", anexo.NomeArquivo);
        Assert.Equal(usuarioId, anexo.EnviadoPorId);
        Assert.Single(storage.Arquivos);
        var chamada = Assert.Single(auditoria.Chamadas);
        Assert.Equal("AnexarDocumento", chamada.Acao);
        Assert.Equal(nameof(EntidadeAnexo.ContaPagar), chamada.TipoEntidade);
        Assert.Equal(contaId, chamada.EntidadeId);
    }

    [Fact]
    public async Task ListarAsync_retorna_so_os_anexos_da_entidade()
    {
        var (gerenciador, _, _, _, usuarioId) = await CriarAsync();
        var conta1 = Guid.NewGuid();
        var conta2 = Guid.NewGuid();
        await gerenciador.AnexarAsync(Novo(conta1), usuarioId);
        await gerenciador.AnexarAsync(Novo(conta1), usuarioId);
        await gerenciador.AnexarAsync(Novo(conta2), usuarioId);

        var lista = await gerenciador.ListarAsync(EntidadeAnexo.ContaPagar, conta1);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task BaixarAsync_recupera_conteudo_original()
    {
        var (gerenciador, _, _, _, usuarioId) = await CriarAsync();
        var anexo = await gerenciador.AnexarAsync(Novo(Guid.NewGuid(), texto: "byte-a-byte"), usuarioId);

        var download = await gerenciador.BaixarAsync(anexo.Id);

        Assert.NotNull(download);
        using var reader = new StreamReader(download!.Conteudo);
        Assert.Equal("byte-a-byte", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ExcluirAsync_remove_linha_arquivo_e_audita()
    {
        var (gerenciador, storage, auditoria, db, usuarioId) = await CriarAsync();
        var anexo = await gerenciador.AnexarAsync(Novo(Guid.NewGuid()), usuarioId);
        auditoria.Chamadas.Clear();

        var resultado = await gerenciador.ExcluirAsync(anexo.Id, usuarioId);

        Assert.True(resultado.Sucesso);
        Assert.Empty(storage.Arquivos);
        Assert.Equal(0, await db.Anexos.CountAsync());
        Assert.Equal("ExcluirDocumento", Assert.Single(auditoria.Chamadas).Acao);
    }

    [Fact]
    public async Task ExcluirAsync_anexo_inexistente_retorna_falha()
    {
        var (gerenciador, _, _, _, usuarioId) = await CriarAsync();

        var resultado = await gerenciador.ExcluirAsync(Guid.NewGuid(), usuarioId);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ExcluirAsync_quando_delete_fisico_falha_mantem_o_registro_no_banco()
    {
        // O arquivo físico é apagado ANTES do registro no banco — se o
        // delete físico falhar, a operação inteira falha e nada muda,
        // em vez de deixar um arquivo órfão em disco sem que ninguém
        // perceba (achado da auditoria de qualidade).
        var (gerenciador, storage, auditoria, db, usuarioId) = await CriarAsync();
        var anexo = await gerenciador.AnexarAsync(Novo(Guid.NewGuid()), usuarioId);
        auditoria.Chamadas.Clear();
        storage.FalharAoExcluir = true;

        var resultado = await gerenciador.ExcluirAsync(anexo.Id, usuarioId);

        Assert.False(resultado.Sucesso);
        Assert.Equal(1, await db.Anexos.CountAsync());
        Assert.Empty(auditoria.Chamadas);
    }
}
