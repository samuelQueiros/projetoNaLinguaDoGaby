using System.Text;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Anexos;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.DocumentosIa;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErpFinanceiro.Tests.DocumentosIa;

public class GerenciadorDocumentosImportadosTests
{
    private sealed class FilaFalsa : IFilaProcessamentoDocumentos
    {
        public List<Guid> Enfileirados { get; } = new();

        public ValueTask EnfileirarAsync(Guid documentoImportadoId, CancellationToken ct = default)
        {
            Enfileirados.Add(documentoImportadoId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Guid> DesenfileirarAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class LeitorFalso(ResultadoLeituraDocumento resultado) : ILeitorDocumentos
    {
        public Task<ResultadoLeituraDocumento> LerAsync(Stream conteudo, string nomeArquivo, string tipoConteudo, CancellationToken ct = default)
            => Task.FromResult(resultado);
    }

    private sealed record Cenario(
        AppDbContext Db,
        GerenciadorDocumentosImportados Gerenciador,
        FilaFalsa Fila,
        ArmazenamentoAnexosFalso Storage,
        Usuario Admin,
        Usuario Financeiro,
        Fornecedor Fornecedor);

    private static async Task<Cenario> PrepararAsync(ResultadoLeituraDocumento? resultadoLeitura = null)
    {
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var storage = new ArmazenamentoAnexosFalso();
        var fila = new FilaFalsa();
        var auditoria = new RegistradorAuditoriaFalso();

        var admin = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, nameof(PerfilUsuario.Administrador), "Administrador Teste");
        var financeiro = await IdentityTestHelpers.CriarUsuarioComPapelAsync(db, userManager, nameof(PerfilUsuario.Financeiro), "Financeiro Teste");

        var fornecedor = new Fornecedor { Id = Guid.NewGuid(), RazaoSocial = "Fornecedor Teste", CnpjCpf = "12345678000199" };
        db.Fornecedores.Add(fornecedor);
        await db.SaveChangesAsync();

        var leitor = new LeitorFalso(resultadoLeitura
            ?? new ResultadoLeituraDocumento(TipoDocumentoDetectado.NaoIdentificado, 0m, Array.Empty<CampoLido>()));

        var contasPagar = new GerenciadorContasPagar(db, auditoria, userManager);
        var anexos = new GerenciadorAnexos(db, storage, auditoria, NullLogger<GerenciadorAnexos>.Instance);

        var gerenciador = new GerenciadorDocumentosImportados(
            db, storage, fila, leitor, contasPagar, anexos, auditoria, userManager,
            new NullLogger<GerenciadorDocumentosImportados>());

        return new Cenario(db, gerenciador, fila, storage, admin, financeiro, fornecedor);
    }

    private static ArquivoEnviado ArquivoFalso(string nome = "comprovante.pdf") =>
        new(new MemoryStream(Encoding.UTF8.GetBytes("conteudo-de-teste")), nome, "application/pdf");

    [Fact]
    public async Task EnviarAsync_cria_documento_recebido_e_enfileira()
    {
        var c = await PrepararAsync();

        var resultado = await c.Gerenciador.EnviarAsync([ArquivoFalso(), ArquivoFalso("nf.pdf")], c.Financeiro.Id);

        Assert.Equal(2, resultado.Criados.Count);
        Assert.Empty(resultado.Falhas);
        Assert.All(resultado.Criados, d => Assert.Equal(StatusImportacaoDocumento.Recebido, d.Status));
        Assert.Equal(2, c.Fila.Enfileirados.Count);
        Assert.Equal(2, c.Storage.Arquivos.Count);
    }

    [Fact]
    public async Task EnviarAsync_com_um_arquivo_invalido_no_lote_nao_descarta_os_demais()
    {
        var c = await PrepararAsync();
        c.Storage.NomesQueDevemFalhar.Add("virus.exe");

        var resultado = await c.Gerenciador.EnviarAsync(
            [ArquivoFalso(), ArquivoFalso("virus.exe"), ArquivoFalso("nf.pdf")], c.Financeiro.Id);

        Assert.Equal(2, resultado.Criados.Count);
        var falha = Assert.Single(resultado.Falhas);
        Assert.Equal("virus.exe", falha.NomeArquivo);
        Assert.Contains("comprovante.pdf", resultado.Criados.Select(d => d.NomeArquivo));
        Assert.Contains("nf.pdf", resultado.Criados.Select(d => d.NomeArquivo));
        Assert.Equal(2, c.Fila.Enfileirados.Count);
    }

    [Fact]
    public async Task ProcessarAsync_com_stub_deixa_documento_aguardando_revisao()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();

        await c.Gerenciador.ProcessarAsync(doc.Id);

        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(StatusImportacaoDocumento.AguardandoRevisao, atualizado.Status);
        Assert.NotNull(atualizado.ProcessadoEm);
    }

    [Fact]
    public async Task ProcessarAsync_guarda_campos_extraidos()
    {
        var resultado = new ResultadoLeituraDocumento(
            TipoDocumentoDetectado.ComprovantePagamento, 0.82m,
            [new CampoLido("valor", "1530.00", 0.98m), new CampoLido("fornecedor", "Fornecedor Teste", 0.6m)]);
        var c = await PrepararAsync(resultado);
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();

        await c.Gerenciador.ProcessarAsync(doc.Id);

        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(TipoDocumentoDetectado.ComprovantePagamento, atualizado.TipoDetectado);
        Assert.Equal(0.82m, atualizado.ConfiancaGeral);
        Assert.Equal(2, atualizado.Campos.Count);
        Assert.Contains(atualizado.Campos, x => x.Nome == "valor" && x.Valor == "1530.00");
    }

    [Fact]
    public async Task ProcessarAsync_marca_falha_quando_leitor_falha()
    {
        var c = await PrepararAsync(ResultadoLeituraDocumento.Falha("ilegível"));
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();

        await c.Gerenciador.ProcessarAsync(doc.Id);

        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(StatusImportacaoDocumento.Falha, atualizado.Status);
        Assert.Equal("ilegível", atualizado.MensagemErro);
    }

    [Fact]
    public async Task AprovarAsync_sem_papel_administrador_e_recusado()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);

        var resultado = await c.Gerenciador.AprovarAsync(doc.Id, RevisaoValida(c.Fornecedor.Id), c.Financeiro.Id);

        Assert.False(resultado.Operacao.Sucesso);
        Assert.Empty(await c.Db.ContasPagar.ToListAsync());
    }

    [Fact]
    public async Task AprovarAsync_como_administrador_cria_conta_e_anexa_arquivo()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);

        var resultado = await c.Gerenciador.AprovarAsync(doc.Id, RevisaoValida(c.Fornecedor.Id), c.Admin.Id);

        Assert.True(resultado.Operacao.Sucesso);
        Assert.NotNull(resultado.Conta);

        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(StatusImportacaoDocumento.Aprovado, atualizado.Status);
        Assert.Equal(resultado.Conta!.Id, atualizado.ContaPagarId);
        Assert.Equal(c.Admin.Id, atualizado.RevisadoPorId);

        var anexo = await c.Db.Anexos.AsNoTracking()
            .SingleAsync(a => a.EntidadeTipo == EntidadeAnexo.ContaPagar && a.EntidadeId == resultado.Conta.Id);
        Assert.Equal("comprovante.pdf", anexo.NomeArquivo);
    }

    [Fact]
    public async Task AprovarAsync_recusa_documento_que_nao_esta_aguardando_revisao()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        // não processado — continua em Recebido

        var resultado = await c.Gerenciador.AprovarAsync(doc.Id, RevisaoValida(c.Fornecedor.Id), c.Admin.Id);

        Assert.False(resultado.Operacao.Sucesso);
    }

    [Fact]
    public async Task RejeitarAsync_exige_motivo_e_marca_rejeitado()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);

        var semMotivo = await c.Gerenciador.RejeitarAsync(doc.Id, "  ", c.Admin.Id);
        Assert.False(semMotivo.Sucesso);

        var comMotivo = await c.Gerenciador.RejeitarAsync(doc.Id, "Documento duplicado", c.Admin.Id);
        Assert.True(comMotivo.Sucesso);

        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(StatusImportacaoDocumento.Rejeitado, atualizado.Status);
        Assert.Equal("Documento duplicado", atualizado.MotivoRejeicao);
    }

    [Fact]
    public async Task ReprocessarAsync_recoloca_documento_com_falha_na_fila()
    {
        var c = await PrepararAsync(ResultadoLeituraDocumento.Falha("timeout"));
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);
        c.Fila.Enfileirados.Clear();

        var resultado = await c.Gerenciador.ReprocessarAsync(doc.Id, c.Financeiro.Id);

        Assert.True(resultado.Sucesso);
        Assert.Contains(doc.Id, c.Fila.Enfileirados);
        var atualizado = await c.Db.DocumentosImportados.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Equal(StatusImportacaoDocumento.Recebido, atualizado.Status);
        Assert.Null(atualizado.MensagemErro);
    }

    [Fact]
    public async Task ReprocessarAsync_recusa_documento_que_nao_falhou()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);

        var resultado = await c.Gerenciador.ReprocessarAsync(doc.Id, c.Financeiro.Id);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ExcluirAsync_sem_papel_administrador_e_recusado()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();

        var resultado = await c.Gerenciador.ExcluirAsync(doc.Id, c.Financeiro.Id);

        Assert.False(resultado.Sucesso);
        Assert.NotNull(await c.Db.DocumentosImportados.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doc.Id));
    }

    [Fact]
    public async Task ExcluirAsync_como_administrador_remove_registro_e_arquivo_fisico()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        var caminho = doc.CaminhoArmazenamento;

        var resultado = await c.Gerenciador.ExcluirAsync(doc.Id, c.Admin.Id);

        Assert.True(resultado.Sucesso);
        Assert.Null(await c.Db.DocumentosImportados.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doc.Id));
        Assert.DoesNotContain(caminho, c.Storage.Arquivos.Keys);
    }

    [Fact]
    public async Task ExcluirAsync_recusa_documento_ja_aprovado()
    {
        var c = await PrepararAsync();
        var doc = (await c.Gerenciador.EnviarAsync([ArquivoFalso()], c.Financeiro.Id)).Criados.Single();
        await c.Gerenciador.ProcessarAsync(doc.Id);
        await c.Gerenciador.AprovarAsync(doc.Id, RevisaoValida(c.Fornecedor.Id), c.Admin.Id);

        var resultado = await c.Gerenciador.ExcluirAsync(doc.Id, c.Admin.Id);

        Assert.False(resultado.Sucesso);
        Assert.NotNull(await c.Db.DocumentosImportados.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doc.Id));
    }

    [Fact]
    public async Task ListarPaginadoAsync_pagina_no_banco_e_devolve_total_real()
    {
        var c = await PrepararAsync();
        for (var i = 0; i < 5; i++)
        {
            await c.Gerenciador.EnviarAsync([ArquivoFalso($"doc{i}.pdf")], c.Financeiro.Id);
        }

        var pagina1 = await c.Gerenciador.ListarPaginadoAsync(new FiltroDocumentosImportados(TamanhoPagina: 2, Pagina: 1));
        var pagina2 = await c.Gerenciador.ListarPaginadoAsync(new FiltroDocumentosImportados(TamanhoPagina: 2, Pagina: 2));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(3, pagina1.TotalPaginas);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.Empty(pagina1.Itens.Select(d => d.Id).Intersect(pagina2.Itens.Select(d => d.Id)));
    }

    [Fact]
    public async Task ObterIndicadoresAsync_conta_por_status_no_banco_todo_nao_so_na_pagina()
    {
        var c = await PrepararAsync(ResultadoLeituraDocumento.Falha("ilegível"));
        await c.Gerenciador.EnviarAsync([ArquivoFalso("a.pdf"), ArquivoFalso("b.pdf")], c.Financeiro.Id);
        var docs = (await c.Db.DocumentosImportados.ToListAsync());
        foreach (var d in docs)
        {
            await c.Gerenciador.ProcessarAsync(d.Id);
        }

        var indicadores = await c.Gerenciador.ObterIndicadoresAsync(null);

        Assert.Equal(2, indicadores.Total);
        Assert.Equal(2, indicadores.ComErro);
        Assert.Equal(0, indicadores.AguardandoRevisao);
    }

    [Fact]
    public async Task ListarAsync_com_busca_filtra_por_nome_do_arquivo()
    {
        var c = await PrepararAsync();
        await c.Gerenciador.EnviarAsync([ArquivoFalso("nota-fiscal-123.pdf"), ArquivoFalso("boleto-agua.pdf")], c.Financeiro.Id);

        var resultado = await c.Gerenciador.ListarAsync(new FiltroDocumentosImportados(Busca: "nota-fiscal"));

        var encontrado = Assert.Single(resultado);
        Assert.Equal("nota-fiscal-123.pdf", encontrado.NomeArquivo);
    }

    private static RevisaoDocumentoInput RevisaoValida(Guid fornecedorId) =>
        new(fornecedorId, "Despesa do comprovante", null, null,
            DateOnly.FromDateTime(DateTime.Today), 1530.00m, 0m, 0m, 0m, null);
}
