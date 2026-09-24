using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AcessoIndividualPorAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AplicacoesProva_CodigoAcesso",
                table: "AplicacoesProva");

            migrationBuilder.DropColumn(
                name: "CodigoAcesso",
                table: "AplicacoesProva");

            migrationBuilder.CreateTable(
                name: "AcessosAlunoAplicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoProvaId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    CodigoAcesso = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcessosAlunoAplicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcessosAlunoAplicacao_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcessosAlunoAplicacao_AplicacoesProva_AplicacaoProvaId",
                        column: x => x.AplicacaoProvaId,
                        principalTable: "AplicacoesProva",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAlunoAplicacao_AlunoId",
                table: "AcessosAlunoAplicacao",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAlunoAplicacao_AplicacaoProvaId_AlunoId",
                table: "AcessosAlunoAplicacao",
                columns: new[] { "AplicacaoProvaId", "AlunoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAlunoAplicacao_CodigoAcesso",
                table: "AcessosAlunoAplicacao",
                column: "CodigoAcesso",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcessosAlunoAplicacao");

            migrationBuilder.AddColumn<string>(
                name: "CodigoAcesso",
                table: "AplicacoesProva",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AplicacoesProva_CodigoAcesso",
                table: "AplicacoesProva",
                column: "CodigoAcesso",
                unique: true);
        }
    }
}
