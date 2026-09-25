using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasFiscais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: true),
                    Numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Serie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Emissao = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: true),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentroCustoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscais", x => x.Id);
                    table.CheckConstraint("CK_NotasFiscais_Valor", "\"Valor\" >= 0");
                    table.ForeignKey(
                        name: "FK_NotasFiscais_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasFiscais_CentrosDeCusto_CentroCustoId",
                        column: x => x.CentroCustoId,
                        principalTable: "CentrosDeCusto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasFiscais_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotasFiscais_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_CategoriaId",
                table: "NotasFiscais",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_CentroCustoId",
                table: "NotasFiscais",
                column: "CentroCustoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_ContaPagarId",
                table: "NotasFiscais",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_Emissao",
                table: "NotasFiscais",
                column: "Emissao");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_FornecedorId",
                table: "NotasFiscais",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_FornecedorId_Numero",
                table: "NotasFiscais",
                columns: new[] { "FornecedorId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_Numero",
                table: "NotasFiscais",
                column: "Numero");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasFiscais");
        }
    }
}
