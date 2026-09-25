using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoImportado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosImportados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CaminhoArmazenamento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoDetectado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConfiancaGeral = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    Campos = table.Column<string>(type: "jsonb", nullable: false),
                    MensagemErro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EnviadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevisadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosImportados", x => x.Id);
                    table.CheckConstraint("CK_DocumentosImportados_ConfiancaGeral", "\"ConfiancaGeral\" >= 0 AND \"ConfiancaGeral\" <= 1");
                    table.CheckConstraint("CK_DocumentosImportados_TamanhoBytes", "\"TamanhoBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_DocumentosImportados_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentosImportados_Usuarios_EnviadoPorId",
                        column: x => x.EnviadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosImportados_Usuarios_RevisadoPorId",
                        column: x => x.RevisadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosImportados_ContaPagarId",
                table: "DocumentosImportados",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosImportados_EnviadoPorId",
                table: "DocumentosImportados",
                column: "EnviadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosImportados_RevisadoPorId",
                table: "DocumentosImportados",
                column: "RevisadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosImportados_Status",
                table: "DocumentosImportados",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosImportados");
        }
    }
}
