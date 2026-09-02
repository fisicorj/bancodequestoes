using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class RestringirExclusaoQuestaoEmProva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProvasQuestoes_Questoes_QuestaoId",
                table: "ProvasQuestoes");

            migrationBuilder.AddForeignKey(
                name: "FK_ProvasQuestoes_Questoes_QuestaoId",
                table: "ProvasQuestoes",
                column: "QuestaoId",
                principalTable: "Questoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProvasQuestoes_Questoes_QuestaoId",
                table: "ProvasQuestoes");

            migrationBuilder.AddForeignKey(
                name: "FK_ProvasQuestoes_Questoes_QuestaoId",
                table: "ProvasQuestoes",
                column: "QuestaoId",
                principalTable: "Questoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
