using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CriaConfiguracaoIaSalva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesIa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Provedor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Modelo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TimeoutSegundos = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesIa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesIa_Usuarios_AtualizadoPorId",
                        column: x => x.AtualizadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesIa_AtualizadoPorId",
                table: "ConfiguracoesIa",
                column: "AtualizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesIa_Finalidade",
                table: "ConfiguracoesIa",
                column: "Finalidade",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesIa");
        }
    }
}
