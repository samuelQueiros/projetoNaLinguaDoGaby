using System.Threading.Channels;
using ErpFinanceiro.Application.DocumentosIa;

namespace ErpFinanceiro.Infrastructure.DocumentosIa;

/// <summary>
/// Fila in-process de documentos a processar (<see cref="IFilaProcessamentoDocumentos"/>).
/// <c>Channel</c> ilimitado — o upload nunca bloqueia esperando o worker
/// (seção 9 do escopo). Singleton: um canal para toda a aplicação,
/// consumido pelo <see cref="ProcessadorDocumentosHostedService"/>.
///
/// A fila não sobrevive a um restart do processo. Rede de segurança: o
/// worker também varre documentos que ficaram em <c>Recebido</c> na
/// inicialização (ver o hosted service).
/// </summary>
public sealed class FilaProcessamentoDocumentos : IFilaProcessamentoDocumentos
{
    private readonly Channel<Guid> canal = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnfileirarAsync(Guid documentoImportadoId, CancellationToken ct = default) =>
        canal.Writer.WriteAsync(documentoImportadoId, ct);

    public ValueTask<Guid> DesenfileirarAsync(CancellationToken ct) =>
        canal.Reader.ReadAsync(ct);
}
