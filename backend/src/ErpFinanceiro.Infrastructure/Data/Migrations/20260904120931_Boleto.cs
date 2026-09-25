using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Boleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boletos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    LinhaDigitavel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodigoBarras = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    Banco = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boletos", x => x.Id);
                    table.CheckConstraint("CK_Boletos_Valor", "\"Valor\" >= 0");
                    table.ForeignKey(
                        name: "FK_Boletos_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Boletos_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boletos_ContaPagarId",
                table: "Boletos",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletos_FornecedorId",
                table: "Boletos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletos_Numero",
                table: "Boletos",
                column: "Numero");

            migrationBuilder.CreateIndex(
                name: "IX_Boletos_Status",
                table: "Boletos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Boletos_Vencimento",
                table: "Boletos",
                column: "Vencimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Boletos");
        }
    }
}
