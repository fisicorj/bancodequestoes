using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class RestringirExclusaoAssuntoDisciplina : Migration
    {
        /// <inheritdoc />
        // Auditoria de 09/09/2026: Questao.AssuntoId e Assunto.DisciplinaId nunca tiveram
        // OnDelete explícito, então caíam na convenção implícita do EF (Cascade pra FK
        // não-anulável) — excluir um Assunto apagava em cadeia todas as suas Questões, e
        // excluir uma Disciplina apagava seus Assuntos + Questões, tudo sem aviso. Troca pra
        // Restrict: o banco passa a recusar a exclusão (igual já acontece com Questao usada
        // em Prova), forçando o professor a mover/excluir o conteúdo dependente primeiro.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assuntos_Disciplinas_DisciplinaId",
                table: "Assuntos");

            migrationBuilder.DropForeignKey(
                name: "FK_Questoes_Assuntos_AssuntoId",
                table: "Questoes");

            migrationBuilder.AddForeignKey(
                name: "FK_Assuntos_Disciplinas_DisciplinaId",
                table: "Assuntos",
                column: "DisciplinaId",
                principalTable: "Disciplinas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questoes_Assuntos_AssuntoId",
                table: "Questoes",
                column: "AssuntoId",
                principalTable: "Assuntos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assuntos_Disciplinas_DisciplinaId",
                table: "Assuntos");

            migrationBuilder.DropForeignKey(
                name: "FK_Questoes_Assuntos_AssuntoId",
                table: "Questoes");

            migrationBuilder.AddForeignKey(
                name: "FK_Assuntos_Disciplinas_DisciplinaId",
                table: "Assuntos",
                column: "DisciplinaId",
                principalTable: "Disciplinas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questoes_Assuntos_AssuntoId",
                table: "Questoes",
                column: "AssuntoId",
                principalTable: "Assuntos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
