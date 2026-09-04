using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Seguranca;
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
    private readonly CriptografiaAes256 criptografia;

    public AppDbContext(DbContextOptions<AppDbContext> options, CriptografiaAes256 criptografia)
        : base(options)
    {
        this.criptografia = criptografia;
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<CentroCusto> CentrosDeCusto => Set<CentroCusto>();

    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();

    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();

    public DbSet<DadosBancariosFornecedor> DadosBancariosFornecedores => Set<DadosBancariosFornecedor>();

    public DbSet<ContaBancariaEmpresa> ContasBancariasEmpresa => Set<ContaBancariaEmpresa>();

    public DbSet<Cartao> Cartoes => Set<Cartao>();

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

        builder.Entity<FormaPagamento>(b =>
        {
            b.ToTable("FormasPagamento");
            b.Property(f => f.Nome).IsRequired().HasMaxLength(100);
            b.Property(f => f.Descricao).HasMaxLength(500);
            b.Property(f => f.Ativo).HasDefaultValue(true);
            b.Property(f => f.CriadoEm).HasDefaultValueSql("now()");
            b.HasIndex(f => f.Nome).IsUnique().HasFilter("\"Ativo\" = true");

            // Seed (Passo 8b, seção 4 do escopo) — data fixa (não now()) para
            // o snapshot da migration ficar determinístico entre builds.
            var seedEm = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            b.HasData(
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000001"), Nome = "PIX", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000002"), Nome = "Transferência bancária", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000003"), Nome = "TED", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000004"), Nome = "DOC", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000005"), Nome = "Boleto", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000006"), Nome = "Cartão de crédito", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000007"), Nome = "Cartão de débito", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000008"), Nome = "Débito automático", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000009"), Nome = "Dinheiro", CriadoEm = seedEm },
                new FormaPagamento { Id = new Guid("00000000-0000-0000-0001-000000000010"), Nome = "Outros", CriadoEm = seedEm }
            );
        });

        builder.Entity<Fornecedor>(b =>
        {
            b.ToTable("Fornecedores");
            b.Property(f => f.RazaoSocial).IsRequired().HasMaxLength(200);
            b.Property(f => f.NomeFantasia).HasMaxLength(200);
            // Normalizado (só dígitos) na conversão, não só no caso de uso —
            // garante que "12.345.678/0001-99" e "12345678000199" nunca
            // coexistam como fornecedores "diferentes" no índice único,
            // mesmo que algo grave via EF sem passar por
            // GerenciadorFornecedores (recomendação do db-schema-reviewer).
            b.Property(f => f.CnpjCpf)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(v => SomenteDigitos(v), v => v);
            b.Property(f => f.InscricaoEstadual).HasMaxLength(30);
            b.Property(f => f.Endereco).HasMaxLength(300);
            b.Property(f => f.Telefone).HasMaxLength(30);
            b.Property(f => f.Email).HasMaxLength(200);
            b.Property(f => f.ContatoResponsavel).HasMaxLength(200);
            b.Property(f => f.Observacoes).HasMaxLength(2000);
            b.Property(f => f.CriadoEm).HasDefaultValueSql("now()");

            // Único só entre os não excluídos (exclusão lógica via
            // ExcluidoEm) — mesmo raciocínio do índice parcial de
            // Categoria/CentroCusto (Passo 5).
            b.HasIndex(f => f.CnpjCpf).IsUnique().HasFilter("\"ExcluidoEm\" IS NULL");

            // Índice avulso em ExcluidoEm: filtros de listagem/relatório de
            // ContaPagar (Passo 14+) vão consultar fornecedores ativos por
            // nome, não só por CNPJ — recomendação do db-schema-reviewer.
            b.HasIndex(f => f.ExcluidoEm);

            b.HasOne(f => f.FormaPagamentoPadrao)
                .WithMany()
                .HasForeignKey(f => f.FormaPagamentoPadraoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DadosBancariosFornecedor>(b =>
        {
            b.ToTable("DadosBancariosFornecedores");
            b.Property(d => d.Banco).IsRequired().HasMaxLength(150);
            b.Property(d => d.Agencia).IsRequired().HasMaxLength(20);
            b.Property(d => d.CriadoEm).HasDefaultValueSql("now()");

            // Conta e ChavePix cifrados em repouso (AES-256-GCM — CLAUDE.md
            // seção 3). A conversão roda em toda leitura/escrita via EF;
            // ChavePix é nullable, então precisa tratar null sem cifrar. O
            // contexto (AAD) amarra o cifrado à coluna — ver observação de
            // limitação em CriptografiaAes256.
            b.Property(d => d.Conta)
                .IsRequired()
                .HasMaxLength(500) // cifrado ocupa mais espaço que o valor original
                .HasConversion(
                    v => criptografia.Cifrar(v, "DadosBancariosFornecedor.Conta"),
                    v => criptografia.Decifrar(v, "DadosBancariosFornecedor.Conta"));

            b.Property(d => d.ChavePix)
                .HasMaxLength(500)
                .HasConversion(
                    v => v == null ? null : criptografia.Cifrar(v, "DadosBancariosFornecedor.ChavePix"),
                    v => v == null ? null : criptografia.Decifrar(v, "DadosBancariosFornecedor.ChavePix"));

            b.HasOne(d => d.Fornecedor)
                .WithMany(f => f.DadosBancarios)
                .HasForeignKey(d => d.FornecedorId)
                .OnDelete(DeleteBehavior.Cascade);
            // Cascade é rede de segurança para exclusão física fora do fluxo
            // normal (script de correção, teste) — Fornecedor nunca é
            // excluído fisicamente pela aplicação (só ExcluidoEm), então
            // isso não faz parte de nenhum caso de uso real. Nunca expor um
            // "excluir fornecedor" físico em Application (db-schema-reviewer,
            // Passo 8/9).

            b.HasIndex(d => d.FornecedorId);
        });

        builder.Entity<ContaBancariaEmpresa>(b =>
        {
            b.ToTable("ContasBancariasEmpresa");
            b.Property(c => c.Banco).IsRequired().HasMaxLength(150);
            b.Property(c => c.Agencia).IsRequired().HasMaxLength(20);
            b.Property(c => c.Apelido).IsRequired().HasMaxLength(100);
            b.Property(c => c.Ativo).HasDefaultValue(true);
            b.Property(c => c.CriadoEm).HasDefaultValueSql("now()");

            // Mesma lógica de cifragem de DadosBancariosFornecedor — é
            // dado bancário sensível mesmo sendo da própria empresa, não
            // de terceiro (CLAUDE.md seção 3).
            b.Property(c => c.Conta)
                .IsRequired()
                .HasMaxLength(500)
                .HasConversion(
                    v => criptografia.Cifrar(v, "ContaBancariaEmpresa.Conta"),
                    v => criptografia.Decifrar(v, "ContaBancariaEmpresa.Conta"));
        });

        builder.Entity<Cartao>(b =>
        {
            b.ToTable("Cartoes");
            b.Property(c => c.InstituicaoFinanceira).IsRequired().HasMaxLength(150);
            b.Property(c => c.Bandeira).IsRequired().HasMaxLength(50);
            b.Property(c => c.Apelido).IsRequired().HasMaxLength(100);
            b.Property(c => c.UltimosQuatroDigitos).IsRequired().HasMaxLength(4);
            b.Property(c => c.Limite).HasColumnType("numeric(14,2)");
            b.Property(c => c.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(c => c.Responsavel)
                .WithMany()
                .HasForeignKey(c => c.ResponsavelId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(c => c.ResponsavelId);

            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_DiaFechamento", "\"DiaFechamento\" BETWEEN 1 AND 31"));
            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_DiaVencimento", "\"DiaVencimento\" BETWEEN 1 AND 31"));
            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_Limite", "\"Limite\" >= 0"));
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

    /// <summary>
    /// Normaliza CNPJ/CPF removendo pontuação antes de gravar — garante que
    /// "12.345.678/0001-99" e "12345678000199" nunca coexistam como
    /// fornecedores "diferentes" no índice único, mesmo se algo gravar via
    /// EF sem passar por GerenciadorFornecedores (recomendação do
    /// db-schema-reviewer, Passo 8/9).
    /// </summary>
    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}
