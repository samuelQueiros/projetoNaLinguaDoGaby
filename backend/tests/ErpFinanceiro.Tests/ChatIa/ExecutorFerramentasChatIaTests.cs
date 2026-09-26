using System.Text.Json;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.ChatIa;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Tests.ChatIa;

public class ExecutorFerramentasChatIaTests
{
    // Cada fake implementa só os métodos de leitura de verdade; os de
    // escrita levantam NotSupportedException — se o executor algum dia
    // chamar um deles por engano, o teste falha na hora, não silenciosamente.

    private sealed class PainelFalso : IPainelFinanceiro
    {
        public IndicadoresPainel Retorno { get; set; } = new(
            1000m, 5, 200m, 1, 50m, 300m, 900m, 400m, 5000m, 100m, 0m, 0,
            new List<VencimentoProximo> { new(Guid.NewGuid(), "Fornecedor X", "Aluguel", DateOnly.FromDateTime(DateTime.Today), 500m) });

        public Task<IndicadoresPainel> ObterAsync() => Task.FromResult(Retorno);
    }

    private sealed class ContasPagarFalso : IGerenciadorContasPagar
    {
        public FiltroContasPagar? UltimoFiltro { get; private set; }
        public List<ContaPagar> Retorno { get; set; } = new();

        public Task<ResultadoContaPagar> CriarAsync(ContaPagarInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> EditarAsync(Guid id, ContaPagarInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId) => throw new NotSupportedException();
        public Task<ContaPagar?> ObterAsync(Guid id) => throw new NotSupportedException();

        public Task<IReadOnlyList<ContaPagar>> ListarAsync(FiltroContasPagar filtro)
        {
            UltimoFiltro = filtro;
            return Task.FromResult<IReadOnlyList<ContaPagar>>(Retorno);
        }

        public Task<ResultadoPaginado<ContaPagar>> ListarPaginadoAsync(FiltroContasPagar filtro) => throw new NotSupportedException();
    }

    private sealed class FornecedoresFalso : IGerenciadorFornecedores
    {
        public List<Fornecedor> Retorno { get; set; } = new();

        public Task<ResultadoCriacao<Fornecedor>> CriarAsync(CriarFornecedorInput input) => throw new NotSupportedException();
        public Task<ResultadoOperacao> EditarAsync(Guid id, CriarFornecedorInput input) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ExcluirAsync(Guid id) => throw new NotSupportedException();
        public Task<Fornecedor?> ObterAsync(Guid id) => throw new NotSupportedException();
        public Task<ResultadoOperacao> AdicionarDadosBancariosAsync(Guid fornecedorId, DadosBancariosInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> EditarDadosBancariosAsync(Guid dadosBancariosId, DadosBancariosInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> RemoverDadosBancariosAsync(Guid dadosBancariosId, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoCriacao<ContratoFornecedor>> AdicionarContratoAsync(Guid fornecedorId, NovoContratoInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContratoFornecedor>> ListarContratosAsync(Guid fornecedorId) => throw new NotSupportedException();
        public Task<ContratoParaDownload?> BaixarContratoAsync(Guid contratoId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> RemoverContratoAsync(Guid contratoId, Guid usuarioId) => throw new NotSupportedException();

        public Task<IReadOnlyList<Fornecedor>> ListarAsync(bool incluirExcluidos = false) =>
            Task.FromResult<IReadOnlyList<Fornecedor>>(Retorno);
    }

    private sealed class DocumentosFalso : IGerenciadorDocumentosImportados
    {
        public FiltroDocumentosImportados? UltimoFiltro { get; private set; }
        public List<DocumentoImportado> Retorno { get; set; } = new();
        public DocumentoImportado? RetornoObter { get; set; }

        public Task<ResultadoEnvioDocumentos> EnviarAsync(IReadOnlyList<ArquivoEnviado> arquivos, Guid usuarioId) => throw new NotSupportedException();
        public Task ProcessarAsync(Guid documentoImportadoId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ReprocessarAsync(Guid id, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoContaPagar> AprovarAsync(Guid id, RevisaoDocumentoInput dados, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> RejeitarAsync(Guid id, string motivo, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoPaginado<DocumentoImportado>> ListarPaginadoAsync(FiltroDocumentosImportados filtro) => throw new NotSupportedException();
        public Task<IndicadoresDocumentosImportados> ObterIndicadoresAsync(Guid? enviadoPorId) => throw new NotSupportedException();

        public Task<DocumentoImportado?> ObterAsync(Guid id) => Task.FromResult(RetornoObter);

        public Task<IReadOnlyList<DocumentoImportado>> ListarAsync(FiltroDocumentosImportados filtro)
        {
            UltimoFiltro = filtro;
            return Task.FromResult<IReadOnlyList<DocumentoImportado>>(Retorno);
        }
    }

    private sealed class NotasFiscaisFalso : IGerenciadorNotasFiscais
    {
        public FiltroNotasFiscais? UltimoFiltro { get; private set; }
        public List<NotaFiscal> Retorno { get; set; } = new();

        public Task<ResultadoCriacao<NotaFiscal>> CriarAsync(NotaFiscalInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> EditarAsync(Guid id, NotaFiscalInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId) => throw new NotSupportedException();
        public Task<NotaFiscal?> ObterAsync(Guid id) => throw new NotSupportedException();

        public Task<IReadOnlyList<NotaFiscal>> ListarAsync(FiltroNotasFiscais filtro)
        {
            UltimoFiltro = filtro;
            return Task.FromResult<IReadOnlyList<NotaFiscal>>(Retorno);
        }
    }

    private sealed class BoletosFalso : IGerenciadorBoletos
    {
        public FiltroBoletos? UltimoFiltro { get; private set; }
        public List<Boleto> Retorno { get; set; } = new();

        public Task<ResultadoCriacao<Boleto>> CriarAsync(BoletoInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> EditarAsync(Guid id, BoletoInput input, Guid usuarioId) => throw new NotSupportedException();
        public Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId) => throw new NotSupportedException();
        public Task<Boleto?> ObterAsync(Guid id) => throw new NotSupportedException();

        public Task<IReadOnlyList<Boleto>> ListarAsync(FiltroBoletos filtro)
        {
            UltimoFiltro = filtro;
            return Task.FromResult<IReadOnlyList<Boleto>>(Retorno);
        }
    }

    private sealed record Cenario(
        ExecutorFerramentasChatIa Executor,
        PainelFalso Painel,
        ContasPagarFalso ContasPagar,
        FornecedoresFalso Fornecedores,
        DocumentosFalso Documentos,
        NotasFiscaisFalso NotasFiscais,
        BoletosFalso Boletos,
        AppDbContext Db,
        UserManager<Usuario> UserManager);

    private static Task<Cenario> PrepararAsync()
    {
        var painel = new PainelFalso();
        var contasPagar = new ContasPagarFalso();
        var fornecedores = new FornecedoresFalso();
        var documentos = new DocumentosFalso();
        var notasFiscais = new NotasFiscaisFalso();
        var boletos = new BoletosFalso();
        var db = AppDbContextFactory.CriarEmMemoria();
        var userManager = IdentityTestHelpers.CriarUserManager(db);
        var executor = new ExecutorFerramentasChatIa(painel, contasPagar, fornecedores, documentos, notasFiscais, boletos, userManager);
        return Task.FromResult(new Cenario(executor, painel, contasPagar, fornecedores, documentos, notasFiscais, boletos, db, userManager));
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task Ferramenta_desconhecida_devolve_erro_sem_lancar_excecao()
    {
        var c = await PrepararAsync();
        var resultado = await c.Executor.ExecutarAsync("ferramenta_que_nao_existe", Args("{}"), Guid.NewGuid(), default);

        var json = JsonSerializer.Serialize(resultado);
        Assert.Contains("desconhecida", json);
    }

    [Fact]
    public async Task ConsultarIndicadores_devolve_dados_do_painel()
    {
        var c = await PrepararAsync();
        var resultado = await c.Executor.ExecutarAsync("consultar_indicadores_financeiros", Args("{}"), Guid.NewGuid(), default);

        var json = JsonSerializer.Serialize(resultado);
        Assert.Contains("1000", json);
        Assert.Contains("Fornecedor X", json);
    }

    [Fact]
    public async Task ListarContasAPagar_sem_fornecedor_nao_filtra_por_fornecedor()
    {
        var c = await PrepararAsync();
        await c.Executor.ExecutarAsync("listar_contas_a_pagar", Args("""{"statusFinanceiro":"Vencida"}"""), Guid.NewGuid(), default);

        Assert.Null(c.ContasPagar.UltimoFiltro!.FornecedorId);
        Assert.Equal(StatusFinanceiro.Vencida, c.ContasPagar.UltimoFiltro!.StatusFinanceiro);
    }

    [Fact]
    public async Task ListarContasAPagar_com_fornecedor_inexistente_devolve_erro_sem_chamar_listar()
    {
        var c = await PrepararAsync();
        c.Fornecedores.Retorno = new List<Fornecedor> { new() { Id = Guid.NewGuid(), RazaoSocial = "Outra Empresa LTDA" } };

        var resultado = await c.Executor.ExecutarAsync(
            "listar_contas_a_pagar", Args("""{"fornecedorNome":"Fornecedor Que Nao Existe"}"""), Guid.NewGuid(), default);

        Assert.Null(c.ContasPagar.UltimoFiltro); // ListarAsync nunca foi chamado
        Assert.Contains("erro", JsonSerializer.Serialize(resultado));
    }

    [Fact]
    public async Task ListarContasAPagar_resolve_nome_do_fornecedor_para_id()
    {
        var c = await PrepararAsync();
        var fornecedorId = Guid.NewGuid();
        c.Fornecedores.Retorno = new List<Fornecedor> { new() { Id = fornecedorId, RazaoSocial = "Distribuidora ACME LTDA" } };

        await c.Executor.ExecutarAsync("listar_contas_a_pagar", Args("""{"fornecedorNome":"acme"}"""), Guid.NewGuid(), default);

        Assert.Equal(fornecedorId, c.ContasPagar.UltimoFiltro!.FornecedorId);
    }

    [Fact]
    public async Task ListarContasAPagar_interpreta_datas_de_vencimento()
    {
        var c = await PrepararAsync();
        await c.Executor.ExecutarAsync(
            "listar_contas_a_pagar",
            Args("""{"vencimentoInicial":"2026-01-01","vencimentoFinal":"2026-01-31"}"""),
            Guid.NewGuid(), default);

        Assert.Equal(new DateOnly(2026, 1, 1), c.ContasPagar.UltimoFiltro!.VencimentoInicial);
        Assert.Equal(new DateOnly(2026, 1, 31), c.ContasPagar.UltimoFiltro!.VencimentoFinal);
    }

    [Fact]
    public async Task ListarFornecedores_filtra_por_nome_contem()
    {
        var c = await PrepararAsync();
        c.Fornecedores.Retorno = new List<Fornecedor>
        {
            new() { Id = Guid.NewGuid(), RazaoSocial = "Distribuidora ACME LTDA" },
            new() { Id = Guid.NewGuid(), RazaoSocial = "Outra Empresa LTDA" },
        };

        var resultado = await c.Executor.ExecutarAsync("listar_fornecedores", Args("""{"nomeContem":"acme"}"""), Guid.NewGuid(), default);

        var json = JsonSerializer.Serialize(resultado);
        Assert.Contains("ACME", json);
        Assert.DoesNotContain("Outra Empresa", json);
    }

    [Fact]
    public async Task ListarDocumentosPendentes_forca_status_aguardando_revisao_mesmo_sem_o_modelo_pedir()
    {
        var c = await PrepararAsync();
        await c.Executor.ExecutarAsync("listar_documentos_pendentes_revisao", Args("{}"), Guid.NewGuid(), default);

        Assert.Equal(StatusImportacaoDocumento.AguardandoRevisao, c.Documentos.UltimoFiltro!.Status);
    }

    [Fact]
    public async Task ListarDocumentosPendentes_com_apenasDoUsuarioAtual_filtra_pelo_usuario_que_perguntou()
    {
        var c = await PrepararAsync();
        var usuarioId = Guid.NewGuid();
        await c.Executor.ExecutarAsync("listar_documentos_pendentes_revisao", Args("""{"apenasDoUsuarioAtual":true}"""), usuarioId, default);

        Assert.Equal(usuarioId, c.Documentos.UltimoFiltro!.EnviadoPorId);
    }

    [Fact]
    public async Task ConsultarDocumentoImportado_com_id_invalido_devolve_erro()
    {
        var c = await PrepararAsync();
        var resultado = await c.Executor.ExecutarAsync("consultar_documento_importado", Args("""{"documentoId":"nao-e-um-guid"}"""), Guid.NewGuid(), default);

        Assert.Contains("erro", JsonSerializer.Serialize(resultado));
    }

    [Fact]
    public async Task ConsultarDocumentoImportado_com_id_valido_do_proprio_usuario_devolve_detalhe()
    {
        var c = await PrepararAsync();
        var id = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        c.Documentos.RetornoObter = new DocumentoImportado
        {
            Id = id, NomeArquivo = "nota.pdf", Status = StatusImportacaoDocumento.AguardandoRevisao, EnviadoPorId = usuarioId,
        };

        var resultado = await c.Executor.ExecutarAsync("consultar_documento_importado", Args($$"""{"documentoId":"{{id}}"}"""), usuarioId, default);

        Assert.Contains("nota.pdf", JsonSerializer.Serialize(resultado));
    }

    [Fact]
    public async Task ConsultarDocumentoImportado_de_outro_usuario_devolve_erro_nao_o_detalhe()
    {
        // Achado da auditoria de segurança: o chat era um segundo caminho
        // pro mesmo vazamento já corrigido em CentralDocumentos.razor —
        // qualquer usuário podia consultar o detalhe de um documento de
        // qualquer outro usuário sabendo (ou adivinhando) o ID.
        var c = await PrepararAsync();
        var id = Guid.NewGuid();
        var dono = Guid.NewGuid();
        var outroUsuario = Guid.NewGuid();
        c.Documentos.RetornoObter = new DocumentoImportado
        {
            Id = id, NomeArquivo = "nota-sigilosa.pdf", Status = StatusImportacaoDocumento.AguardandoRevisao, EnviadoPorId = dono,
        };

        var resultado = await c.Executor.ExecutarAsync("consultar_documento_importado", Args($$"""{"documentoId":"{{id}}"}"""), outroUsuario, default);

        var json = JsonSerializer.Serialize(resultado);
        Assert.DoesNotContain("nota-sigilosa.pdf", json);
        Assert.Contains("erro", json);
    }

    [Fact]
    public async Task ConsultarDocumentoImportado_como_Administrador_ve_documento_de_qualquer_usuario()
    {
        var c = await PrepararAsync();
        var id = Guid.NewGuid();
        var dono = Guid.NewGuid();
        var admin = await IdentityTestHelpers.CriarUsuarioComPapelAsync(c.Db, c.UserManager, "Administrador", "Admin Teste");
        c.Documentos.RetornoObter = new DocumentoImportado
        {
            Id = id, NomeArquivo = "nota.pdf", Status = StatusImportacaoDocumento.AguardandoRevisao, EnviadoPorId = dono,
        };

        var resultado = await c.Executor.ExecutarAsync("consultar_documento_importado", Args($$"""{"documentoId":"{{id}}"}"""), admin.Id, default);

        Assert.Contains("nota.pdf", JsonSerializer.Serialize(resultado));
    }

    [Fact]
    public async Task ListarDocumentosPendentes_usuario_nao_administrador_so_ve_os_proprios_mesmo_sem_pedir()
    {
        // Antes do fix, "apenasDoUsuarioAtual" era opcional e, por padrão
        // (false), listava documentos de TODOS os usuários pra qualquer
        // papel — mesmo achado da auditoria de segurança.
        var c = await PrepararAsync();
        var usuarioId = Guid.NewGuid();

        await c.Executor.ExecutarAsync("listar_documentos_pendentes_revisao", Args("{}"), usuarioId, default);

        Assert.Equal(usuarioId, c.Documentos.UltimoFiltro!.EnviadoPorId);
    }

    [Fact]
    public async Task ListarDocumentosPendentes_como_Administrador_sem_pedir_apenasDoUsuario_ve_de_todos()
    {
        var c = await PrepararAsync();
        var admin = await IdentityTestHelpers.CriarUsuarioComPapelAsync(c.Db, c.UserManager, "Administrador", "Admin Teste");

        await c.Executor.ExecutarAsync("listar_documentos_pendentes_revisao", Args("{}"), admin.Id, default);

        Assert.Null(c.Documentos.UltimoFiltro!.EnviadoPorId);
    }

    [Fact]
    public async Task ListarBoletos_interpreta_status()
    {
        var c = await PrepararAsync();
        await c.Executor.ExecutarAsync("listar_boletos", Args("""{"status":"Vencido"}"""), Guid.NewGuid(), default);

        Assert.Equal(StatusBoleto.Vencido, c.Boletos.UltimoFiltro!.Status);
    }

    [Fact]
    public async Task ListarNotasFiscais_repassa_numero()
    {
        var c = await PrepararAsync();
        await c.Executor.ExecutarAsync("listar_notas_fiscais", Args("""{"numero":"12345"}"""), Guid.NewGuid(), default);

        Assert.Equal("12345", c.NotasFiscais.UltimoFiltro!.Numero);
    }
}
