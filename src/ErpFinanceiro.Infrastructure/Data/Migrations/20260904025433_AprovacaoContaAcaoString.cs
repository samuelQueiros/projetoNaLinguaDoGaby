using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AprovacaoContaAcaoString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AprovacoesConta_MotivoObrigatorioSeRejeitada",
                table: "AprovacoesConta");

            migrationBuilder.AlterColumn<string>(
                name: "Acao",
                table: "AprovacoesConta",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AprovacoesConta_MotivoObrigatorioSeRejeitada",
                table: "AprovacoesConta",
                sql: "\"Acao\" <> 'Rejeitada' OR (\"Motivo\" IS NOT NULL AND length(trim(\"Motivo\")) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AprovacoesConta_MotivoObrigatorioSeRejeitada",
                table: "AprovacoesConta");

            migrationBuilder.AlterColumn<int>(
                name: "Acao",
                table: "AprovacoesConta",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AprovacoesConta_MotivoObrigatorioSeRejeitada",
                table: "AprovacoesConta",
                sql: "\"Acao\" <> 1 OR (\"Motivo\" IS NOT NULL AND length(trim(\"Motivo\")) > 0)");
        }
    }
}
