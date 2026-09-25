using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FornecedorFormaPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormasPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormasPagamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fornecedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazaoSocial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeFantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CnpjCpf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InscricaoEstadual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContatoResponsavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FormaPagamentoPadraoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExcluidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornecedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fornecedores_FormasPagamento_FormaPagamentoPadraoId",
                        column: x => x.FormaPagamentoPadraoId,
                        principalTable: "FormasPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DadosBancariosFornecedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Banco = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Agencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Conta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ChavePix = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Principal = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DadosBancariosFornecedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DadosBancariosFornecedores_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FormasPagamento",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "Descricao", "Nome" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "PIX" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Transferência bancária" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "TED" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "DOC" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Boleto" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cartão de crédito" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cartão de débito" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Débito automático" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Dinheiro" },
                    { new Guid("00000000-0000-0000-0001-000000000010"), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Outros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DadosBancariosFornecedores_FornecedorId",
                table: "DadosBancariosFornecedores",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_FormasPagamento_Nome",
                table: "FormasPagamento",
                column: "Nome",
                unique: true,
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_CnpjCpf",
                table: "Fornecedores",
                column: "CnpjCpf",
                unique: true,
                filter: "\"ExcluidoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_FormaPagamentoPadraoId",
                table: "Fornecedores",
                column: "FormaPagamentoPadraoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DadosBancariosFornecedores");

            migrationBuilder.DropTable(
                name: "Fornecedores");

            migrationBuilder.DropTable(
                name: "FormasPagamento");
        }
    }
}
