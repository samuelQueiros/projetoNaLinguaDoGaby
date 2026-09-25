using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Web.Servicos;

/// <summary>
/// Worker que consome a fila de documentos (<see cref="IFilaProcessamentoDocumentos"/>)
/// e roda a extração via <see cref="IGerenciadorDocumentosImportados.ProcessarAsync"/>.
/// Um documento por vez — leitura de documento é I/O-bound e, com o serviço
/// Python real, custa dinheiro por chamada; serializar evita rajada.
///
/// Na inicialização faz uma varredura dos documentos que ficaram em
/// <see cref="StatusImportacaoDocumento.Recebido"/> ou
/// <see cref="StatusImportacaoDocumento.Processando"/> (fila é in-process e
/// não sobrevive a restart — docs/modulo-ia-documentos.md).
/// </summary>
public sealed class ProcessadorDocumentosHostedService(
    IFilaProcessamentoDocumentos fila,
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessadorDocumentosHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReprocessarPendentesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid id;
            try
            {
                id = await fila.DesenfileirarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessarUmAsync(id, stoppingToken);
        }
    }

    private async Task ProcessarUmAsync(Guid documentoId, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var gerenciador = scope.ServiceProvider.GetRequiredService<IGerenciadorDocumentosImportados>();
            await gerenciador.ProcessarAsync(documentoId, ct);
        }
        catch (Exception ex)
        {
            // ProcessarAsync já marca Falha no banco em erro esperado; aqui é
            // só a rede de segurança para não derrubar o loop do worker.
            logger.LogError(ex, "Erro não tratado ao processar o documento {DocumentoId}.", documentoId);
        }
    }

    private async Task ReprocessarPendentesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendentes = await db.DocumentosImportados
                .Where(d => d.Status == StatusImportacaoDocumento.Recebido
                         || d.Status == StatusImportacaoDocumento.Processando)
                .Select(d => d.Id)
                .ToListAsync(ct);

            if (pendentes.Count == 0)
            {
                return;
            }

            logger.LogInformation("Reenfileirando {Quantidade} documento(s) pendente(s) na inicialização.", pendentes.Count);
            foreach (var id in pendentes)
            {
                await fila.EnfileirarAsync(id, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao varrer documentos pendentes na inicialização.");
        }
    }
}
