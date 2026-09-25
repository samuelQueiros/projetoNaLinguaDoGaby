using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AprovacaoConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AprovacoesConta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AprovacoesConta", x => x.Id);
                    table.CheckConstraint("CK_AprovacoesConta_MotivoObrigatorioSeRejeitada", "\"Acao\" <> 1 OR (\"Motivo\" IS NOT NULL AND length(trim(\"Motivo\")) > 0)");
                    table.ForeignKey(
                        name: "FK_AprovacoesConta_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AprovacoesConta_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AprovacoesConta_ContaPagarId",
                table: "AprovacoesConta",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_AprovacoesConta_UsuarioId",
                table: "AprovacoesConta",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AprovacoesConta");
        }
    }
}
