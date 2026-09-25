using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContaPagarEAjustesRevisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContasPagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentroCustoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorOriginal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Desconto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Juros = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Multa = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ValorFinal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    FormaPagamentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusAprovacao = table.Column<int>(type: "integer", nullable: false),
                    StatusFinanceiro = table.Column<int>(type: "integer", nullable: false),
                    CriadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MotivoCancelamentoRejeicao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExcluidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasPagar", x => x.Id);
                    table.CheckConstraint("CK_ContasPagar_Desconto", "\"Desconto\" >= 0");
                    table.CheckConstraint("CK_ContasPagar_Juros", "\"Juros\" >= 0");
                    table.CheckConstraint("CK_ContasPagar_Multa", "\"Multa\" >= 0");
                    table.CheckConstraint("CK_ContasPagar_ValorFinal", "\"ValorFinal\" >= 0");
                    table.CheckConstraint("CK_ContasPagar_ValorOriginal", "\"ValorOriginal\" >= 0");
                    table.ForeignKey(
                        name: "FK_ContasPagar_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_CentrosDeCusto_CentroCustoId",
                        column: x => x.CentroCustoId,
                        principalTable: "CentrosDeCusto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_FormasPagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "FormasPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_Usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasBancariasEmpresa_Apelido",
                table: "ContasBancariasEmpresa",
                column: "Apelido",
                unique: true,
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContasBancariasEmpresa_Ativo",
                table: "ContasBancariasEmpresa",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Cartoes_Status",
                table: "Cartoes",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cartoes_UltimosQuatroDigitos",
                table: "Cartoes",
                sql: "\"UltimosQuatroDigitos\" ~ '^[0-9]{4}$'");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_CategoriaId",
                table: "ContasPagar",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_CentroCustoId",
                table: "ContasPagar",
                column: "CentroCustoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_CriadoPorId",
                table: "ContasPagar",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_ExcluidoEm",
                table: "ContasPagar",
                column: "ExcluidoEm");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_FormaPagamentoId",
                table: "ContasPagar",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_FornecedorId",
                table: "ContasPagar",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_StatusAprovacao",
                table: "ContasPagar",
                column: "StatusAprovacao");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_StatusFinanceiro",
                table: "ContasPagar",
                column: "StatusFinanceiro");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_Vencimento",
                table: "ContasPagar",
                column: "Vencimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContasPagar");

            migrationBuilder.DropIndex(
                name: "IX_ContasBancariasEmpresa_Apelido",
                table: "ContasBancariasEmpresa");

            migrationBuilder.DropIndex(
                name: "IX_ContasBancariasEmpresa_Ativo",
                table: "ContasBancariasEmpresa");

            migrationBuilder.DropIndex(
                name: "IX_Cartoes_Status",
                table: "Cartoes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cartoes_UltimosQuatroDigitos",
                table: "Cartoes");
        }
    }
}
