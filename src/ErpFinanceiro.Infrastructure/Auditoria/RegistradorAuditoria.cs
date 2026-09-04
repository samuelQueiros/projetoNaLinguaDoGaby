using System.Text.Json;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ErpFinanceiro.Infrastructure.Auditoria;

/// <summary>
/// Implementação real de IRegistradorAuditoria (Passo 21) — substitui
/// RegistradorAuditoriaProvisorio (Passo 16). Serializa valor anterior/novo
/// como JSON (jsonb no Postgres). Nunca passe uma entidade de
/// ErpFinanceiro.Domain diretamente (ex.: a própria ContaPagar) — bloqueado
/// em runtime abaixo, não só por convenção: em Blazor Server o AppDbContext
/// é scoped por circuito, então o EF Core pode popular navegações via
/// relationship fixup (ex.: ContaPagar.Fornecedor) mesmo sem Include na
/// chamada atual, se outra tela do mesmo circuito já tiver carregado esse
/// Fornecedor rastreado — vazando CnpjCpf/dados bancários em texto plano
/// no JSONB do log, que nunca é apagado (achado alto do security-auditor,
/// Passo 21). Sempre montar um objeto anônimo/DTO com só os campos que
/// interessam à auditoria.
/// </summary>
public sealed class RegistradorAuditoria(AppDbContext db, IHttpContextAccessor httpContextAccessor) : IRegistradorAuditoria
{
    private static readonly JsonSerializerOptions OpcoesJson = new() { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    public async Task RegistrarAsync(
        Guid usuarioId, string acao, string tipoEntidade, Guid entidadeId, object? valorAnterior, object? valorNovo)
    {
        GarantirQueNaoEhEntidadeDeDominio(valorAnterior);
        GarantirQueNaoEhEntidadeDeDominio(valorNovo);

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

    /// <summary>
    /// Blindagem estrutural (não só comentário): recusa qualquer valor cujo
    /// tipo pertença ao namespace ErpFinanceiro.Domain — entidades EF nunca
    /// devem ser auditadas cruas, só DTOs/objetos anônimos explícitos.
    /// </summary>
    private static void GarantirQueNaoEhEntidadeDeDominio(object? valor)
    {
        var tipo = valor?.GetType();
        if (tipo?.Namespace?.StartsWith("ErpFinanceiro.Domain", StringComparison.Ordinal) == true)
        {
            throw new InvalidOperationException(
                $"Não é permitido auditar a entidade de domínio '{tipo.Name}' diretamente — risco de vazar " +
                "dados sensíveis via relationship fixup do EF Core (AppDbContext é scoped por circuito em " +
                "Blazor Server). Monte um objeto anônimo/DTO só com os campos relevantes.");
        }
    }
}
