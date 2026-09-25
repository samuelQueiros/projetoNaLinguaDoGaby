using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpFinanceiro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Pagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorPago = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    FormaPagamentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaBancariaEmpresaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CartaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComprovanteAnexoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MotivoEstorno = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RegistradoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.CheckConstraint("CK_Pagamentos_ContaOuCartaoExclusivo", "((\"ContaBancariaEmpresaId\" IS NOT NULL)::int + (\"CartaoId\" IS NOT NULL)::int) = 1");
                    table.CheckConstraint("CK_Pagamentos_ValorPago", "\"ValorPago\" > 0");
                    table.ForeignKey(
                        name: "FK_Pagamentos_Cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "Cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_ContasBancariasEmpresa_ContaBancariaEmpresaId",
                        column: x => x.ContaBancariaEmpresaId,
                        principalTable: "ContasBancariasEmpresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_FormasPagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "FormasPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Usuarios_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_CartaoId",
                table: "Pagamentos",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_ContaBancariaEmpresaId",
                table: "Pagamentos",
                column: "ContaBancariaEmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_ContaPagarId",
                table: "Pagamentos",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_FormaPagamentoId",
                table: "Pagamentos",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_RegistradoPorId",
                table: "Pagamentos",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_Status",
                table: "Pagamentos",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pagamentos");
        }
    }
}
