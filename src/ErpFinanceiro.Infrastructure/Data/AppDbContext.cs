using System.Text.Json;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();

    public DbSet<AprovacaoConta> AprovacoesConta => Set<AprovacaoConta>();

    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();

    public DbSet<Anexo> Anexos => Set<Anexo>();

    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();

    public DbSet<Boleto> Boletos => Set<Boleto>();

    public DbSet<DocumentoImportado> DocumentosImportados => Set<DocumentoImportado>();

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
            // Nota (db-schema-reviewer, Passo 12): Conta é cifrada com nonce
            // aleatório — um índice único nela nunca detectaria duplicata
            // (o mesmo valor gera ciphertexts diferentes a cada gravação).
            // Duplicidade de conta, se necessária, fica para checagem na
            // aplicação (decifrando) ou uma coluna de hash determinístico
            // dedicada — não resolvido ainda, volume baixo o suficiente
            // para não bloquear o MVP.

            // Único só entre as ativas — mesmo padrão de Categoria/CentroCusto/FormaPagamento.
            b.HasIndex(c => c.Apelido).IsUnique().HasFilter("\"Ativo\" = true");
            b.HasIndex(c => c.Ativo);
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
            b.HasIndex(c => c.Status);

            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_DiaFechamento", "\"DiaFechamento\" BETWEEN 1 AND 31"));
            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_DiaVencimento", "\"DiaVencimento\" BETWEEN 1 AND 31"));
            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_Limite", "\"Limite\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_Cartoes_UltimosQuatroDigitos", "\"UltimosQuatroDigitos\" ~ '^[0-9]{4}$'"));
        });

        builder.Entity<ContaPagar>(b =>
        {
            b.ToTable("ContasPagar");
            b.Property(c => c.Descricao).IsRequired().HasMaxLength(300);
            b.Property(c => c.ValorOriginal).HasColumnType("numeric(14,2)");
            b.Property(c => c.Desconto).HasColumnType("numeric(14,2)");
            b.Property(c => c.Juros).HasColumnType("numeric(14,2)");
            b.Property(c => c.Multa).HasColumnType("numeric(14,2)");
            b.Property(c => c.ValorFinal).HasColumnType("numeric(14,2)");
            b.Property(c => c.MotivoCancelamentoRejeicao).HasMaxLength(1000);
            b.Property(c => c.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(c => c.Fornecedor)
                .WithMany()
                .HasForeignKey(c => c.FornecedorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(c => c.Categoria)
                .WithMany()
                .HasForeignKey(c => c.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(c => c.CentroCusto)
                .WithMany()
                .HasForeignKey(c => c.CentroCustoId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(c => c.FormaPagamento)
                .WithMany()
                .HasForeignKey(c => c.FormaPagamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(c => c.CriadoPor)
                .WithMany()
                .HasForeignKey(c => c.CriadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Índices para os filtros de relatório/dashboard (Passos 28-29 do
            // plano) — fornecedor, vencimento e os dois status são os
            // critérios de busca mais usados.
            b.HasIndex(c => c.FornecedorId);
            b.HasIndex(c => c.Vencimento);
            b.HasIndex(c => c.StatusFinanceiro);
            b.HasIndex(c => c.StatusAprovacao);
            b.HasIndex(c => c.ExcluidoEm);

            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_ValorOriginal", "\"ValorOriginal\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_Desconto", "\"Desconto\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_Juros", "\"Juros\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_Multa", "\"Multa\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_ValorFinal", "\"ValorFinal\" >= 0"));
            // Nenhum fluxo do escopo justifica desconto maior que o valor
            // original (db-schema-reviewer, Passo 14).
            b.ToTable(t => t.HasCheckConstraint("CK_ContasPagar_DescontoMenorQueOriginal", "\"Desconto\" <= \"ValorOriginal\""));

            // Token de concorrência otimista via xmin (coluna de sistema do
            // Postgres, sem migration/coluna nova) — ContaPagar vai ser
            // editada por fluxos concorrentes em breve (aprovação, Passo 18;
            // pagamento, Passo 19); sem isso, dois usuários poderiam agir
            // sobre a mesma conta sem que o segundo saiba que o estado mudou
            // debaixo dele (db-schema-reviewer, Passo 14). UseXminAsConcurrencyToken
            // está obsoleto no Npgsql atual — substituído pela forma padrão do EF Core.
            b.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        });

        builder.Entity<AprovacaoConta>(b =>
        {
            b.ToTable("AprovacoesConta");
            b.Property(a => a.Motivo).HasMaxLength(1000);
            b.Property(a => a.Data).HasDefaultValueSql("now()");

            // Convertido para string (não o int default do EF) para o CHECK
            // abaixo comparar pelo nome, não pela posição do enum — um int
            // travado como "<> 1" quebraria silenciosamente se alguém
            // reordenasse AcaoAprovacao no futuro (db-schema-reviewer, Passo 18).
            b.Property(a => a.Acao).HasConversion<string>().HasMaxLength(20);

            b.HasOne(a => a.ContaPagar)
                .WithMany()
                .HasForeignKey(a => a.ContaPagarId)
                .OnDelete(DeleteBehavior.Restrict); // registro de auditoria pontual — nunca cai em cascata

            b.HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(a => a.ContaPagarId);
            b.HasIndex(a => a.UsuarioId);

            // Motivo obrigatório quando a ação é rejeição — mesma regra que
            // o caso de uso valida em Application, reforçada no schema.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AprovacoesConta_MotivoObrigatorioSeRejeitada",
                "\"Acao\" <> 'Rejeitada' OR (\"Motivo\" IS NOT NULL AND length(trim(\"Motivo\")) > 0)"));
        });

        builder.Entity<Pagamento>(b =>
        {
            b.ToTable("Pagamentos");
            b.Property(p => p.ValorPago).HasColumnType("numeric(14,2)");
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.MotivoEstorno).HasMaxLength(1000);
            b.Property(p => p.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(p => p.ContaPagar)
                .WithMany()
                .HasForeignKey(p => p.ContaPagarId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.FormaPagamento)
                .WithMany()
                .HasForeignKey(p => p.FormaPagamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.ContaBancariaEmpresa)
                .WithMany()
                .HasForeignKey(p => p.ContaBancariaEmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.Cartao)
                .WithMany()
                .HasForeignKey(p => p.CartaoId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.RegistradoPor)
                .WithMany()
                .HasForeignKey(p => p.RegistradoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(p => p.ContaPagarId);
            b.HasIndex(p => p.Status);
            b.HasIndex(p => p.RegistradoPorId);

            // Exatamente um de ContaBancariaEmpresaId/CartaoId — "OU", nunca
            // os dois nem nenhum (Passo 19 do plano).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_Pagamentos_ContaOuCartaoExclusivo",
                "((\"ContaBancariaEmpresaId\" IS NOT NULL)::int + (\"CartaoId\" IS NOT NULL)::int) = 1"));
            b.ToTable(t => t.HasCheckConstraint("CK_Pagamentos_ValorPago", "\"ValorPago\" > 0"));
        });

        builder.Entity<LogAuditoria>(b =>
        {
            b.ToTable("LogsAuditoria");
            b.Property(l => l.Acao).IsRequired().HasMaxLength(100);
            b.Property(l => l.TipoEntidade).IsRequired().HasMaxLength(100);
            b.Property(l => l.Ip).HasMaxLength(45); // IPv6 cabe em 45 chars
            b.Property(l => l.ValorAnteriorJson).HasColumnType("jsonb").HasColumnName("ValorAnterior");
            b.Property(l => l.ValorNovoJson).HasColumnType("jsonb").HasColumnName("ValorNovo");
            b.Property(l => l.Data).HasDefaultValueSql("now()");

            b.HasOne(l => l.Usuario)
                .WithMany()
                .HasForeignKey(l => l.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Índice composto para a timeline por conta (Passo 22) — a
            // consulta típica é WHERE TipoEntidade = X AND EntidadeId = Y
            // ORDER BY Data.
            b.HasIndex(l => new { l.TipoEntidade, l.EntidadeId, l.Data });
            b.HasIndex(l => l.UsuarioId);
        });

        builder.Entity<Anexo>(b =>
        {
            b.ToTable("Anexos");
            b.Property(a => a.EntidadeTipo).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.TipoDocumento).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(300);
            b.Property(a => a.CaminhoArmazenamento).IsRequired().HasMaxLength(400);
            b.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(150);
            b.Property(a => a.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(a => a.EnviadoPor)
                .WithMany()
                .HasForeignKey(a => a.EnviadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Consulta típica: todos os anexos de uma entidade (Passo 24) —
            // WHERE EntidadeTipo = X AND EntidadeId = Y.
            b.HasIndex(a => new { a.EntidadeTipo, a.EntidadeId });
            b.HasIndex(a => a.EnviadoPorId);

            b.ToTable(t => t.HasCheckConstraint("CK_Anexos_TamanhoBytes", "\"TamanhoBytes\" >= 0"));
        });

        builder.Entity<NotaFiscal>(b =>
        {
            b.ToTable("NotasFiscais");
            b.Property(n => n.Numero).IsRequired().HasMaxLength(60);
            b.Property(n => n.Serie).HasMaxLength(20);
            b.Property(n => n.Valor).HasColumnType("numeric(14,2)");
            b.Property(n => n.Observacoes).HasMaxLength(2000);
            b.Property(n => n.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(n => n.Fornecedor).WithMany().HasForeignKey(n => n.FornecedorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(n => n.ContaPagar).WithMany().HasForeignKey(n => n.ContaPagarId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(n => n.Categoria).WithMany().HasForeignKey(n => n.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(n => n.CentroCusto).WithMany().HasForeignKey(n => n.CentroCustoId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(n => n.FornecedorId);
            b.HasIndex(n => n.ContaPagarId);
            // Busca por número (seção 7 do escopo) — não é único: fornecedores
            // diferentes podem emitir NFs com o mesmo número.
            b.HasIndex(n => n.Numero);
            b.HasIndex(n => new { n.FornecedorId, n.Numero });
            b.HasIndex(n => n.Emissao);

            b.ToTable(t => t.HasCheckConstraint("CK_NotasFiscais_Valor", "\"Valor\" >= 0"));
        });

        builder.Entity<Boleto>(b =>
        {
            b.ToTable("Boletos");
            b.Property(x => x.Numero).IsRequired().HasMaxLength(60);
            b.Property(x => x.LinhaDigitavel).IsRequired().HasMaxLength(100);
            b.Property(x => x.CodigoBarras).HasMaxLength(60);
            b.Property(x => x.Valor).HasColumnType("numeric(14,2)");
            b.Property(x => x.Banco).HasMaxLength(150);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Observacoes).HasMaxLength(2000);
            b.Property(x => x.CriadoEm).HasDefaultValueSql("now()");

            b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ContaPagar).WithMany().HasForeignKey(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.FornecedorId);
            b.HasIndex(x => x.ContaPagarId);
            // Busca por número (seção 8 do escopo) — não é único: fornecedores
            // diferentes podem emitir boletos com o mesmo número.
            b.HasIndex(x => x.Numero);
            b.HasIndex(x => x.Vencimento);
            b.HasIndex(x => x.Status);

            b.ToTable(t => t.HasCheckConstraint("CK_Boletos_Valor", "\"Valor\" >= 0"));
        });

        builder.Entity<DocumentoImportado>(b =>
        {
            b.ToTable("DocumentosImportados");
            b.Property(d => d.NomeArquivo).IsRequired().HasMaxLength(300);
            b.Property(d => d.CaminhoArmazenamento).IsRequired().HasMaxLength(400);
            b.Property(d => d.TipoConteudo).IsRequired().HasMaxLength(150);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(d => d.TipoDetectado).HasConversion<string>().HasMaxLength(30);
            b.Property(d => d.ConfiancaGeral).HasColumnType("numeric(5,4)");
            b.Property(d => d.MensagemErro).HasMaxLength(2000);
            b.Property(d => d.MotivoRejeicao).HasMaxLength(1000);
            b.Property(d => d.TextoExtraido).HasColumnType("text");
            b.Property(d => d.Resumo).HasMaxLength(500);
            b.Property(d => d.CriadoEm).HasDefaultValueSql("now()");

            // Campos extraídos como jsonb: são só para exibir na revisão, não
            // há consulta por campo — mesmo raciocínio do ValorAnterior/Novo
            // de LogAuditoria. ValueComparer via serialização porque é uma
            // coleção mutável de referência (exigido pelo EF Core).
            var jsonOpts = (JsonSerializerOptions?)null;
            b.Property(d => d.Campos)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOpts),
                    v => JsonSerializer.Deserialize<List<CampoExtraido>>(v, jsonOpts) ?? new List<CampoExtraido>())
                .Metadata.SetValueComparer(new ValueComparer<List<CampoExtraido>>(
                    (a, c) => JsonSerializer.Serialize(a, jsonOpts) == JsonSerializer.Serialize(c, jsonOpts),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, jsonOpts).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<CampoExtraido>>(JsonSerializer.Serialize(v, jsonOpts), jsonOpts) ?? new List<CampoExtraido>()));

            b.HasOne(d => d.ContaPagar)
                .WithMany()
                .HasForeignKey(d => d.ContaPagarId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(d => d.EnviadoPor)
                .WithMany()
                .HasForeignKey(d => d.EnviadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(d => d.RevisadoPor)
                .WithMany()
                .HasForeignKey(d => d.RevisadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Consulta típica: a fila de revisão (WHERE Status = 'AguardandoRevisao'
            // ORDER BY CriadoEm) e o histórico por quem enviou.
            b.HasIndex(d => d.Status);
            b.HasIndex(d => d.EnviadoPorId);
            b.HasIndex(d => d.ContaPagarId);

            b.ToTable(t => t.HasCheckConstraint("CK_DocumentosImportados_TamanhoBytes", "\"TamanhoBytes\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_DocumentosImportados_ConfiancaGeral",
                "\"ConfiancaGeral\" >= 0 AND \"ConfiancaGeral\" <= 1"));
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
