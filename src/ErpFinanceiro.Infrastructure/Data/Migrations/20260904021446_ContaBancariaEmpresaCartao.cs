using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContaBancariaEmpresaCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cartoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstituicaoFinanceira = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Bandeira = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Apelido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UltimosQuatroDigitos = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Limite = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    DiaFechamento = table.Column<int>(type: "integer", nullable: false),
                    DiaVencimento = table.Column<int>(type: "integer", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartoes", x => x.Id);
                    table.CheckConstraint("CK_Cartoes_DiaFechamento", "\"DiaFechamento\" BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_Cartoes_DiaVencimento", "\"DiaVencimento\" BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_Cartoes_Limite", "\"Limite\" >= 0");
                    table.ForeignKey(
                        name: "FK_Cartoes_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContasBancariasEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Banco = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Agencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Conta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Apelido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasBancariasEmpresa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cartoes_ResponsavelId",
                table: "Cartoes",
                column: "ResponsavelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cartoes");

            migrationBuilder.DropTable(
                name: "ContasBancariasEmpresa");
        }
    }
}
