using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTextoResumoDocumentoImportado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Resumo",
                table: "DocumentosImportados",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoExtraido",
                table: "DocumentosImportados",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Resumo",
                table: "DocumentosImportados");

            migrationBuilder.DropColumn(
                name: "TextoExtraido",
                table: "DocumentosImportados");
        }
    }
}
