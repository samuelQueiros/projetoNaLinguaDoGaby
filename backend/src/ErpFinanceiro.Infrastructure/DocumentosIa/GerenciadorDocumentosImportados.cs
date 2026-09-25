using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.DocumentosIa;

/// <summary>
/// Casos de uso da Central de Documentos (seção 9 do escopo;
/// docs/modulo-ia-documentos.md). Nenhum lançamento entra como oficial
/// direto da IA — tudo passa pela fila de revisão de um Administrador.
/// </summary>
public sealed class GerenciadorDocumentosImportados(
    AppDbContext db,
    IArmazenamentoAnexos storage,
    IFilaProcessamentoDocumentos fila,
    ILeitorDocumentos leitor,
    IGerenciadorContasPagar gerenciadorContasPagar,
    IGerenciadorAnexos gerenciadorAnexos,
    IRegistradorAuditoria auditoria,
    UserManager<Usuario> userManager,
    ILogger<GerenciadorDocumentosImportados> logger) : IGerenciadorDocumentosImportados
{
    public async Task<ResultadoEnvioDocumentos> EnviarAsync(IReadOnlyList<ArquivoEnviado> arquivos, Guid usuarioId)
    {
        var criados = new List<DocumentoImportado>();
        var falhas = new List<FalhaEnvioDocumento>();

        foreach (var arquivo in arquivos)
        {
            try
            {
                var tipoConteudo = string.IsNullOrWhiteSpace(arquivo.TipoConteudo)
                    ? "application/octet-stream"
                    : arquivo.TipoConteudo;

                var armazenado = await storage.SalvarAsync(arquivo.Conteudo, arquivo.NomeArquivo, tipoConteudo);

                var documento = new DocumentoImportado
                {
                    Id = Guid.NewGuid(),
                    NomeArquivo = Path.GetFileName(arquivo.NomeArquivo),
                    CaminhoArmazenamento = armazenado.CaminhoRelativo,
                    TamanhoBytes = armazenado.TamanhoBytes,
                    TipoConteudo = armazenado.TipoConteudo,
                    Status = StatusImportacaoDocumento.Recebido,
                    EnviadoPorId = usuarioId,
                };

                db.DocumentosImportados.Add(documento);
                await db.SaveChangesAsync();

                await auditoria.RegistrarAsync(usuarioId, "EnviarDocumento", nameof(DocumentoImportado), documento.Id, null,
                    new { documento.NomeArquivo, documento.TamanhoBytes, documento.TipoConteudo });

                await fila.EnfileirarAsync(documento.Id);
                criados.Add(documento);
            }
            catch (InvalidOperationException ex)
            {
                // Extensão não permitida ou tamanho excedido (ArmazenamentoAnexosDisco.SalvarAsync)
                // — um arquivo ruim no meio de um envio múltiplo não pode
                // abortar os demais nem esconder que os outros já foram
                // salvos (achado da auditoria de qualidade). Mensagem já
                // pensada pra tela (extensão/tamanho), sem detalhe interno.
                logger.LogWarning(ex, "Falha ao enviar o arquivo {NomeArquivo} para a Central de Documentos.", arquivo.NomeArquivo);
                falhas.Add(new FalhaEnvioDocumento(arquivo.NomeArquivo, ex.Message));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Falha de armazenamento (disco cheio, permissão, volume
                // indisponível) — diferente de "arquivo ruim": aqui o
                // problema é do servidor, não do que o usuário enviou.
                // ex.Message pode conter caminho físico do servidor, só no log.
                logger.LogError(ex, "Falha de armazenamento ao salvar {NomeArquivo}.", arquivo.NomeArquivo);
                falhas.Add(new FalhaEnvioDocumento(arquivo.NomeArquivo, "Não consegui salvar este arquivo agora — tente de novo em instantes."));
            }
        }

        return new ResultadoEnvioDocumentos(criados, falhas);
    }

    public async Task ProcessarAsync(Guid documentoImportadoId, CancellationToken ct = default)
    {
        var documento = await db.DocumentosImportados.FirstOrDefaultAsync(d => d.Id == documentoImportadoId, ct);
        if (documento is null)
        {
            logger.LogWarning("ProcessarAsync: documento {DocumentoId} não encontrado.", documentoImportadoId);
            return;
        }

        if (documento.Status is not (StatusImportacaoDocumento.Recebido or StatusImportacaoDocumento.Processando))
        {
            // Já revisado/aprovado/rejeitado — reenfileiramento duplicado, ignora.
            return;
        }

        documento.Status = StatusImportacaoDocumento.Processando;
        await db.SaveChangesAsync(ct);

        try
        {
            await using var conteudo = await storage.AbrirAsync(documento.CaminhoArmazenamento, ct);
            var resultado = await leitor.LerAsync(conteudo, documento.NomeArquivo, documento.TipoConteudo, ct);

            if (!resultado.Sucesso)
            {
                documento.Status = StatusImportacaoDocumento.Falha;
                documento.MensagemErro = resultado.Erro;
            }
            else
            {
                documento.TipoDetectado = resultado.TipoDetectado;
                documento.ConfiancaGeral = resultado.ConfiancaGeral;
                documento.Campos = resultado.Campos
                    .Select(c => new CampoExtraido { Nome = c.Nome, Valor = c.Valor, Confianca = c.Confianca })
                    .ToList();
                documento.TextoExtraido = resultado.Texto;
                documento.Resumo = GerarResumo(documento.TipoDetectado, documento.Campos);
                documento.Status = StatusImportacaoDocumento.AguardandoRevisao;
                documento.MensagemErro = null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao processar o documento {DocumentoId}.", documentoImportadoId);
            documento.Status = StatusImportacaoDocumento.Falha;
            // ex.Message (ex.: erro de conectividade com o serviço de IA,
            // com host/porta internos) fica só no log — mostrar isso pra
            // qualquer usuário autenticado era um achado da auditoria de
            // segurança. resultado.Erro (acima) já é uma mensagem pensada
            // pra tela, essa aqui não.
            documento.MensagemErro = "Não consegui processar este documento — tente enviar de novo em instantes.";
        }

        documento.ProcessadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ResultadoOperacao> ReprocessarAsync(Guid id, Guid usuarioId)
    {
        var documento = await db.DocumentosImportados.FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return ResultadoOperacao.Falha("Documento não encontrado.");
        }

        if (documento.Status != StatusImportacaoDocumento.Falha)
        {
            return ResultadoOperacao.Falha("Só é possível reprocessar um documento com falha na leitura.");
        }

        documento.Status = StatusImportacaoDocumento.Recebido;
        documento.MensagemErro = null;
        await db.SaveChangesAsync();

        await fila.EnfileirarAsync(documento.Id);
        return ResultadoOperacao.Ok();
    }

    public async Task<DocumentoImportado?> ObterAsync(Guid id) =>
        await db.DocumentosImportados.AsNoTracking()
            .Include(d => d.EnviadoPor)
            .Include(d => d.RevisadoPor)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<IReadOnlyList<DocumentoImportado>> ListarAsync(FiltroDocumentosImportados filtro)
    {
        var query = ConstruirQuery(filtro);
        return await query.OrderByDescending(d => d.CriadoEm).ToListAsync();
    }

    public async Task<ResultadoPaginado<DocumentoImportado>> ListarPaginadoAsync(FiltroDocumentosImportados filtro)
    {
        var query = ConstruirQuery(filtro);
        var total = await query.CountAsync();

        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = filtro.TamanhoPagina <= 0 ? 20 : Math.Min(filtro.TamanhoPagina, 100);

        var itens = await query
            .OrderByDescending(d => d.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync();

        return new ResultadoPaginado<DocumentoImportado>(itens, total, pagina, tamanho);
    }

    public async Task<IndicadoresDocumentosImportados> ObterIndicadoresAsync(Guid? enviadoPorId)
    {
        var query = db.DocumentosImportados.AsNoTracking().AsQueryable();
        if (enviadoPorId is Guid uid)
        {
            query = query.Where(d => d.EnviadoPorId == uid);
        }

        return new IndicadoresDocumentosImportados(
            Total: await query.CountAsync(),
            AguardandoRevisao: await query.CountAsync(d => d.Status == StatusImportacaoDocumento.AguardandoRevisao),
            ComErro: await query.CountAsync(d => d.Status == StatusImportacaoDocumento.Falha),
            EmProcessamento: await query.CountAsync(d =>
                d.Status == StatusImportacaoDocumento.Recebido || d.Status == StatusImportacaoDocumento.Processando));
    }

    private IQueryable<DocumentoImportado> ConstruirQuery(FiltroDocumentosImportados filtro)
    {
        var query = db.DocumentosImportados.AsNoTracking()
            .Include(d => d.EnviadoPor)
            .AsQueryable();

        if (filtro.Status is StatusImportacaoDocumento status)
        {
            query = query.Where(d => d.Status == status);
        }

        if (filtro.EnviadoPorId is Guid enviadoPor)
        {
            query = query.Where(d => d.EnviadoPorId == enviadoPor);
        }

        if (filtro.TipoDetectado is TipoDocumentoDetectado tipo)
        {
            query = query.Where(d => d.TipoDetectado == tipo);
        }

        if (filtro.ComErro is true)
        {
            query = query.Where(d => d.Status == StatusImportacaoDocumento.Falha);
        }
        else if (filtro.ComErro is false)
        {
            query = query.Where(d => d.Status != StatusImportacaoDocumento.Falha);
        }

        if (filtro.DataInicial is DateOnly inicio)
        {
            var inicioUtc = inicio.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.CriadoEm >= inicioUtc);
        }

        if (filtro.DataFinal is DateOnly fim)
        {
            var fimUtc = fim.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(d => d.CriadoEm <= fimUtc);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            // ToLower().Contains() em vez de EF.Functions.ILike: traduz pra
            // SQL igual no Postgres de produção (LOWER(coluna) LIKE ...) e
            // continua avaliável em LINQ puro nos testes (InMemory provider
            // não traduz funções específicas do Npgsql).
            var busca = filtro.Busca.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.NomeArquivo.ToLower().Contains(busca) ||
                (d.Resumo != null && d.Resumo.ToLower().Contains(busca)) ||
                (d.TextoExtraido != null && d.TextoExtraido.ToLower().Contains(busca)));
        }

        return query;
    }

    public async Task<ResultadoContaPagar> AprovarAsync(Guid id, RevisaoDocumentoInput dados, Guid usuarioId)
    {
        var erroPermissao = await ValidarAdministradorAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return ResultadoContaPagar.Falha(erroPermissao);
        }

        var documento = await db.DocumentosImportados.FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return ResultadoContaPagar.Falha("Documento não encontrado.");
        }

        if (documento.Status != StatusImportacaoDocumento.AguardandoRevisao)
        {
            return ResultadoContaPagar.Falha("Só é possível aprovar um documento que está aguardando revisão.");
        }

        var input = new ContaPagarInput(
            dados.FornecedorId,
            dados.Descricao,
            dados.CategoriaId,
            dados.CentroCustoId,
            dados.Vencimento,
            dados.ValorOriginal,
            dados.Desconto,
            dados.Juros,
            dados.Multa,
            dados.FormaPagamentoId);

        var resultado = await gerenciadorContasPagar.CriarAsync(input, usuarioId);
        if (!resultado.Operacao.Sucesso || resultado.Conta is null)
        {
            return resultado;
        }

        // Anexa o arquivo original à ContaPagar criada, já preenchendo um dos
        // documentos exigidos no Fechamento de Mês.
        await using (var conteudo = await storage.AbrirAsync(documento.CaminhoArmazenamento))
        {
            await gerenciadorAnexos.AnexarAsync(
                new NovoAnexo(
                    EntidadeAnexo.ContaPagar,
                    resultado.Conta.Id,
                    MapearTipoAnexo(documento.TipoDetectado),
                    conteudo,
                    documento.NomeArquivo,
                    documento.TipoConteudo),
                usuarioId);
        }

        documento.Status = StatusImportacaoDocumento.Aprovado;
        documento.ContaPagarId = resultado.Conta.Id;
        documento.RevisadoPorId = usuarioId;
        documento.RevisadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "AprovarDocumento", nameof(DocumentoImportado), documento.Id, null,
            new { ContaPagarId = resultado.Conta.Id, resultado.Conta.ValorFinal });

        return resultado;
    }

    public async Task<ResultadoOperacao> RejeitarAsync(Guid id, string motivo, Guid usuarioId)
    {
        var erroPermissao = await ValidarAdministradorAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return ResultadoOperacao.Falha(erroPermissao);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoOperacao.Falha("Informe o motivo da rejeição.");
        }

        var documento = await db.DocumentosImportados.FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return ResultadoOperacao.Falha("Documento não encontrado.");
        }

        if (documento.Status is not (StatusImportacaoDocumento.AguardandoRevisao or StatusImportacaoDocumento.Falha))
        {
            return ResultadoOperacao.Falha("Só é possível rejeitar um documento aguardando revisão ou com falha.");
        }

        documento.Status = StatusImportacaoDocumento.Rejeitado;
        documento.MotivoRejeicao = motivo.Trim();
        documento.RevisadoPorId = usuarioId;
        documento.RevisadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "RejeitarDocumento", nameof(DocumentoImportado), documento.Id, null,
            new { documento.MotivoRejeicao });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var erroPermissao = await ValidarAdministradorAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return ResultadoOperacao.Falha(erroPermissao);
        }

        var documento = await db.DocumentosImportados.FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return ResultadoOperacao.Falha("Documento não encontrado.");
        }

        if (documento.Status == StatusImportacaoDocumento.Aprovado)
        {
            return ResultadoOperacao.Falha(
                "Este documento já virou uma conta a pagar — excluir apagaria o rastro de onde ela veio. " +
                "Se precisa removê-lo da lista, trate a conta a pagar gerada em vez do documento.");
        }

        // Mesma ordem do GerenciadorAnexos.ExcluirAsync: apaga o arquivo
        // físico antes da linha do banco — se o delete físico falhar
        // (permissão, arquivo em uso), nada muda, em vez de deixar o banco
        // sem o registro e um arquivo órfão em disco.
        try
        {
            await storage.ExcluirAsync(documento.CaminhoArmazenamento);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Falha ao excluir o arquivo físico do documento {DocumentoId} ({Caminho}).", documento.Id, documento.CaminhoArmazenamento);
            return ResultadoOperacao.Falha("Não consegui excluir o arquivo — tente de novo em instantes.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Falha ao excluir o arquivo físico do documento {DocumentoId} ({Caminho}).", documento.Id, documento.CaminhoArmazenamento);
            return ResultadoOperacao.Falha("Não consegui excluir o arquivo — tente de novo em instantes.");
        }

        db.DocumentosImportados.Remove(documento);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "ExcluirDocumento", nameof(DocumentoImportado), documento.Id,
            new { documento.NomeArquivo, documento.Status }, null);

        return ResultadoOperacao.Ok();
    }

    private async Task<string?> ValidarAdministradorAsync(Guid usuarioId)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return "Usuário não encontrado.";
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        return papeis.Contains(nameof(PerfilUsuario.Administrador))
            ? null
            : "Só usuários com perfil Administrador podem revisar a fila de documentos.";
    }

    /// <summary>
    /// Resumo curto e determinístico a partir dos campos já extraídos —
    /// sem chamar LLM (item "leitura sem IA" do escopo). Só concatena o
    /// que já foi lido; se não achou nada, some sozinho (revisão manual).
    /// </summary>
    private static string? GerarResumo(TipoDocumentoDetectado tipo, List<CampoExtraido> campos)
    {
        string? Campo(string nome) => campos
            .FirstOrDefault(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Valor))
            ?.Valor;

        var partes = new List<string>();

        var descricaoTipo = tipo == TipoDocumentoDetectado.NaoIdentificado ? "Documento" : tipo.ToString();
        partes.Add(descricaoTipo);

        if (Campo("fornecedor") is { } fornecedor)
        {
            partes.Add($"de {fornecedor}");
        }

        if (Campo("valor") is { } valor)
        {
            partes.Add($"no valor de R$ {valor}");
        }

        if ((Campo("vencimento") ?? Campo("data")) is { } data)
        {
            partes.Add($"com data {data}");
        }

        if (Campo("numeroNota") is { } numero)
        {
            partes.Add($"(nº {numero})");
        }

        // Só o tipo, sem mais nenhum campo confiável — não vale como resumo.
        return partes.Count > 1 ? string.Join(" ", partes) : null;
    }

    private static TipoDocumentoAnexo MapearTipoAnexo(TipoDocumentoDetectado tipo) => tipo switch
    {
        TipoDocumentoDetectado.ComprovantePagamento => TipoDocumentoAnexo.Comprovante,
        TipoDocumentoDetectado.Contrato => TipoDocumentoAnexo.Contrato,
        TipoDocumentoDetectado.NotaFiscal => TipoDocumentoAnexo.NotaFiscal,
        TipoDocumentoDetectado.Boleto => TipoDocumentoAnexo.Boleto,
        _ => TipoDocumentoAnexo.Outros,
    };
}
