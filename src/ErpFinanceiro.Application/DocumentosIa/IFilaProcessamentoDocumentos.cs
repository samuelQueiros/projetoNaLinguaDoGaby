namespace ErpFinanceiro.Application.DocumentosIa;

/// <summary>
/// Fila assíncrona de documentos a processar (seção 9 do escopo: o upload
/// não trava a tela, pode ser muitos arquivos de uma vez).
///
/// Implementada in-process com <c>System.Threading.Channels</c> +
/// <c>BackgroundService</c> no MVP; a interface permite trocar por uma fila
/// externa (Redis, RabbitMQ) sem mexer nos casos de uso. Só o id trafega
/// pela fila — o worker recarrega o <c>DocumentoImportado</c> do banco.
/// </summary>
public interface IFilaProcessamentoDocumentos
{
    ValueTask EnfileirarAsync(Guid documentoImportadoId, CancellationToken ct = default);

    ValueTask<Guid> DesenfileirarAsync(CancellationToken ct);
}
