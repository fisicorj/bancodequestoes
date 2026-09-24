using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AjustarCascadeAcessoAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcessosAlunoAplicacao_AplicacoesProva_AplicacaoProvaId",
                table: "AcessosAlunoAplicacao");

            migrationBuilder.AddForeignKey(
                name: "FK_AcessosAlunoAplicacao_AplicacoesProva_AplicacaoProvaId",
                table: "AcessosAlunoAplicacao",
                column: "AplicacaoProvaId",
                principalTable: "AplicacoesProva",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcessosAlunoAplicacao_AplicacoesProva_AplicacaoProvaId",
                table: "AcessosAlunoAplicacao");

            migrationBuilder.AddForeignKey(
                name: "FK_AcessosAlunoAplicacao_AplicacoesProva_AplicacaoProvaId",
                table: "AcessosAlunoAplicacao",
                column: "AplicacaoProvaId",
                principalTable: "AplicacoesProva",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
