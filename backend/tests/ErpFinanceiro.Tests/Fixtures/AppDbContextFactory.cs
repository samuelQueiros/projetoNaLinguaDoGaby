using System.Security.Cryptography;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Tests.Fixtures;

/// <summary>
/// Cria um AppDbContext isolado (EF Core InMemory, um banco novo por
/// chamada via Guid) para testes de Application/Infrastructure. Extraído
/// como helper compartilhado — a partir do Passo 6 — para não duplicar essa
/// configuração em cada classe de teste conforme os módulos crescem
/// (recomendação do test-writer).
///
/// Atenção: o provider InMemory NÃO aplica constraints específicas do
/// Postgres (índices únicos parciais/HasFilter, MaxLength, CHECK). Testes
/// que dependem dessas constraints — ex.: unicidade de nome entre
/// categorias ativas, ou a exclusividade conta bancária/cartão em
/// Pagamento (Passo 19) — precisam de um teste de integração contra
/// Postgres real, não deste helper. Ver nota nos Passos 14/15/19 do
/// docs/plano-mvp.md.
/// </summary>
public static class AppDbContextFactory
{
    public static AppDbContext CriarEmMemoria()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // Chave só para teste — nunca reaproveitar para dados reais.
        var chaveDeTeste = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new AppDbContext(options, new CriptografiaAes256(chaveDeTeste));
    }
}
