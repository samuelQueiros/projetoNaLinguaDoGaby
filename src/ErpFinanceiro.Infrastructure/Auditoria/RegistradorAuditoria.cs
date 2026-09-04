using System.Text.Json;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ErpFinanceiro.Infrastructure.Auditoria;

/// <summary>
/// Implementação real de IRegistradorAuditoria (Passo 21) — substitui
/// RegistradorAuditoriaProvisorio (Passo 16). Serializa valor anterior/novo
/// como JSON (jsonb no Postgres). Quem chama é responsável por não passar
/// entidades com campos sensíveis em claro (ex.: nunca passar
/// DadosBancariosFornecedor.Conta/ChavePix decifrados) — ver observação do
/// security-auditor no Passo 21.
/// </summary>
public sealed class RegistradorAuditoria(AppDbContext db, IHttpContextAccessor httpContextAccessor) : IRegistradorAuditoria
{
    private static readonly JsonSerializerOptions OpcoesJson = new() { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    public async Task RegistrarAsync(
        Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo)
    {
        var log = new LogAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Acao = acao,
            TipoEntidade = tipoEntidade,
            EntidadeId = entidadeId,
            ValorAnteriorJson = valorAnterior is null ? null : JsonSerializer.Serialize(valorAnterior, OpcoesJson),
            ValorNovoJson = valorNovo is null ? null : JsonSerializer.Serialize(valorNovo, OpcoesJson),
            Ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Data = DateTime.UtcNow,
        };

        db.LogsAuditoria.Add(log);
        await db.SaveChangesAsync();
    }
}
