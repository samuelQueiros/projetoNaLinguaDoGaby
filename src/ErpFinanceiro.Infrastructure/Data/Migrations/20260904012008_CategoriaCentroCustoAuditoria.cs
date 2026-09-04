using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaCentroCustoAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CentrosDeCusto_Nome",
                table: "CentrosDeCusto");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_Nome",
                table: "Categorias");

            migrationBuilder.AddColumn<DateTime>(
                name: "AtualizadoEm",
                table: "CentrosDeCusto",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "CentrosDeCusto",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "AtualizadoEm",
                table: "Categorias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "Categorias",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "IX_CentrosDeCusto_Nome",
                table: "CentrosDeCusto",
                column: "Nome",
                unique: true,
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nome",
                table: "Categorias",
                column: "Nome",
                unique: true,
                filter: "\"Ativo\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CentrosDeCusto_Nome",
                table: "CentrosDeCusto");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_Nome",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "AtualizadoEm",
                table: "CentrosDeCusto");

            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "CentrosDeCusto");

            migrationBuilder.DropColumn(
                name: "AtualizadoEm",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "Categorias");

            migrationBuilder.CreateIndex(
                name: "IX_CentrosDeCusto_Nome",
                table: "CentrosDeCusto",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nome",
                table: "Categorias",
                column: "Nome",
                unique: true);
        }
    }
}
