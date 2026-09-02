using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AlinhamentoCurricular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CursoId",
                table: "Questoes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiretrizesCurriculares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CursoId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    DiretrizPaiId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiretrizesCurriculares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiretrizesCurriculares_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiretrizesCurriculares_DiretrizesCurriculares_DiretrizPaiId",
                        column: x => x.DiretrizPaiId,
                        principalTable: "DiretrizesCurriculares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesDiretrizes",
                columns: table => new
                {
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    DiretrizCurricularId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesDiretrizes", x => new { x.QuestaoId, x.DiretrizCurricularId });
                    table.ForeignKey(
                        name: "FK_QuestoesDiretrizes_DiretrizesCurriculares_DiretrizCurricula~",
                        column: x => x.DiretrizCurricularId,
                        principalTable: "DiretrizesCurriculares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestoesDiretrizes_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questoes_CursoId",
                table: "Questoes",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_CursoId",
                table: "DiretrizesCurriculares",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_DiretrizPaiId",
                table: "DiretrizesCurriculares",
                column: "DiretrizPaiId");

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_Tipo",
                table: "DiretrizesCurriculares",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_QuestoesDiretrizes_DiretrizCurricularId",
                table: "QuestoesDiretrizes",
                column: "DiretrizCurricularId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questoes_Cursos_CursoId",
                table: "Questoes",
                column: "CursoId",
                principalTable: "Cursos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questoes_Cursos_CursoId",
                table: "Questoes");

            migrationBuilder.DropTable(
                name: "QuestoesDiretrizes");

            migrationBuilder.DropTable(
                name: "DiretrizesCurriculares");

            migrationBuilder.DropIndex(
                name: "IX_Questoes_CursoId",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "CursoId",
                table: "Questoes");
        }
    }
}
