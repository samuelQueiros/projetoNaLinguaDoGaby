using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContaPagarConcorrenciaOtimista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ContasPagar",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContasPagar_DescontoMenorQueOriginal",
                table: "ContasPagar",
                sql: "\"Desconto\" <= \"ValorOriginal\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ContasPagar_DescontoMenorQueOriginal",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ContasPagar");
        }
    }
}
