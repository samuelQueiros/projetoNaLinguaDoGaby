using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Anexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anexos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntidadeTipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CaminhoArmazenamento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnviadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anexos", x => x.Id);
                    table.CheckConstraint("CK_Anexos_TamanhoBytes", "\"TamanhoBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_Anexos_Usuarios_EnviadoPorId",
                        column: x => x.EnviadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anexos_EntidadeTipo_EntidadeId",
                table: "Anexos",
                columns: new[] { "EntidadeTipo", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Anexos_EnviadoPorId",
                table: "Anexos",
                column: "EnviadoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anexos");
        }
    }
}
