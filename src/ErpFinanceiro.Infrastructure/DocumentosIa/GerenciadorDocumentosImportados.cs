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
                // salvos (achado da auditoria de qualidade).
                logger.LogWarning(ex, "Falha ao enviar o arquivo {NomeArquivo} para a Central de Documentos.", arquivo.NomeArquivo);
                falhas.Add(new FalhaEnvioDocumento(arquivo.NomeArquivo, ex.Message));
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

        return await query.OrderByDescending(d => d.CriadoEm).ToListAsync();
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

    private static TipoDocumentoAnexo MapearTipoAnexo(TipoDocumentoDetectado tipo) => tipo switch
    {
        TipoDocumentoDetectado.ComprovantePagamento => TipoDocumentoAnexo.Comprovante,
        TipoDocumentoDetectado.Contrato => TipoDocumentoAnexo.Contrato,
        TipoDocumentoDetectado.NotaFiscal => TipoDocumentoAnexo.NotaFiscal,
        TipoDocumentoDetectado.Boleto => TipoDocumentoAnexo.Boleto,
        _ => TipoDocumentoAnexo.Outros,
    };
}
