using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class Cursos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Provas_Instituicoes_InstituicaoId",
                table: "Provas");

            migrationBuilder.RenameColumn(
                name: "InstituicaoId",
                table: "Provas",
                newName: "CursoId");

            migrationBuilder.RenameIndex(
                name: "IX_Provas_InstituicaoId",
                table: "Provas",
                newName: "IX_Provas_CursoId");

            // O rename acima preserva os valores antigos de InstituicaoId dentro da coluna
            // CursoId, mas esses números apontam pra linhas de "Instituicoes", não de
            // "Cursos" (que ainda nem existe). Zera pra evitar violar a FK nova — o professor
            // reassocia o curso de cada prova depois, na tela de edição.
            migrationBuilder.Sql(@"UPDATE ""Provas"" SET ""CursoId"" = NULL;");

            migrationBuilder.CreateTable(
                name: "Cursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    InstituicaoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cursos_Instituicoes_InstituicaoId",
                        column: x => x.InstituicaoId,
                        principalTable: "Instituicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_InstituicaoId",
                table: "Cursos",
                column: "InstituicaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Provas_Cursos_CursoId",
                table: "Provas",
                column: "CursoId",
                principalTable: "Cursos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Provas_Cursos_CursoId",
                table: "Provas");

            migrationBuilder.DropTable(
                name: "Cursos");

            migrationBuilder.RenameColumn(
                name: "CursoId",
                table: "Provas",
                newName: "InstituicaoId");

            migrationBuilder.RenameIndex(
                name: "IX_Provas_CursoId",
                table: "Provas",
                newName: "IX_Provas_InstituicaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Provas_Instituicoes_InstituicaoId",
                table: "Provas",
                column: "InstituicaoId",
                principalTable: "Instituicoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
