using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Data;

/// <summary>
/// Contexto de dados da aplicação. Expõe o schema do ASP.NET Core Identity
/// já usando a entidade <see cref="Usuario"/> (Passo 4 do plano do MVP) —
/// as demais entidades de domínio (Fornecedor, ContaPagar etc.) entram nos
/// passos seguintes.
/// </summary>
public class AppDbContext : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<CentroCusto> CentrosDeCusto => Set<CentroCusto>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Nomes de tabela em português, para consistência com o restante do
        // schema (convenção da seção 10 do CLAUDE.md). Ajustado antes de
        // qualquer entidade de domínio depender dessas tabelas (Passo 3),
        // para não precisar de uma migration de rename mais adiante.
        builder.Entity<Usuario>().ToTable("Usuarios");
        builder.Entity<IdentityRole<Guid>>().ToTable("Papeis");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UsuarioPapeis");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UsuarioClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("PapelClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UsuarioLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UsuarioTokens");

        // O default `= true` de Usuario.Ativo é só do CLR — sem isso, o EF
        // gera a coluna com DEFAULT false (default(bool)), fazendo qualquer
        // insert fora do UserManager (script, correção manual) criar um
        // usuário inativo silenciosamente. Corrigido por recomendação do
        // db-schema-reviewer no Passo 4.
        builder.Entity<Usuario>().Property(u => u.Ativo).HasDefaultValue(true);
        builder.Entity<Usuario>().Property(u => u.Nome).HasMaxLength(200);

        builder.Entity<Categoria>(b =>
        {
            b.ToTable("Categorias");
            b.Property(c => c.Nome).IsRequired().HasMaxLength(100);
            b.Property(c => c.Descricao).HasMaxLength(500);
            b.Property(c => c.Ativo).HasDefaultValue(true);
            b.Property(c => c.CriadoEm).HasDefaultValueSql("now()");
            // Único só entre os ativos: permite reusar o nome de uma
            // categoria desativada (recomendação do db-schema-reviewer).
            b.HasIndex(c => c.Nome).IsUnique().HasFilter("\"Ativo\" = true");
        });

        builder.Entity<CentroCusto>(b =>
        {
            b.ToTable("CentrosDeCusto");
            b.Property(c => c.Nome).IsRequired().HasMaxLength(100);
            b.Property(c => c.Descricao).HasMaxLength(500);
            b.Property(c => c.Ativo).HasDefaultValue(true);
            b.Property(c => c.CriadoEm).HasDefaultValueSql("now()");
            b.HasIndex(c => c.Nome).IsUnique().HasFilter("\"Ativo\" = true");
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Preenche CriadoEm/AtualizadoEm de toda entidade que implemente
    /// IEntidadeAuditavel, sem cada módulo repetir essa lógica (recomendação
    /// do db-schema-reviewer no Passo 5) — vale para Categoria/CentroCusto
    /// hoje e para as entidades dos próximos passos.
    /// </summary>
    private void AplicarTimestamps()
    {
        var agora = DateTime.UtcNow;
        foreach (var entrada in ChangeTracker.Entries<IEntidadeAuditavel>())
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.Entity.CriadoEm = agora;
            }
            else if (entrada.State == EntityState.Modified)
            {
                entrada.Entity.AtualizadoEm = agora;
            }
        }
    }
}
