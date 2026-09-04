using ErpFinanceiro.Application.Auditoria;

namespace ErpFinanceiro.Tests.Fixtures;

/// <summary>
/// Fake de IRegistradorAuditoria que grava as chamadas recebidas, em vez
/// de só descartar (Task.CompletedTask) — permite testar que operações
/// críticas realmente chamam RegistrarAsync, não só que elas funcionam
/// (recomendação do test-writer, Passo 21: sem isso, remover a chamada de
/// auditoria de um caso de uso não quebraria nenhum teste).
/// </summary>
public sealed class RegistradorAuditoriaFalso : IRegistradorAuditoria
{
    public List<ChamadaAuditoria> Chamadas { get; } = new();

    public Task RegistrarAsync(Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo)
    {
        Chamadas.Add(new ChamadaAuditoria(usuarioId, acao, tipoEntidade, entidadeId, valorAnterior, valorNovo));
        return Task.CompletedTask;
    }
}

public sealed record ChamadaAuditoria(Guid UsuarioId, string Acao, string TipoEntidade, Guid EntidadeId, object? ValorAnterior, object? ValorNovo);
