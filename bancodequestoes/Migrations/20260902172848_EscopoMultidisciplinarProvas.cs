using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class EscopoMultidisciplinarProvas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Provas_Disciplinas_DisciplinaId",
                table: "Provas");

            migrationBuilder.AlterColumn<int>(
                name: "DisciplinaId",
                table: "Provas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "MatrizReferenciaId",
                table: "Provas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoEscopo",
                table: "Provas",
                type: "text",
                nullable: false,
                defaultValue: "Disciplina");

            migrationBuilder.CreateTable(
                name: "ProvasDisciplinas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProvaId = table.Column<int>(type: "integer", nullable: false),
                    DisciplinaId = table.Column<int>(type: "integer", nullable: false),
                    PercentualPlanejado = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvasDisciplinas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvasDisciplinas_Disciplinas_DisciplinaId",
                        column: x => x.DisciplinaId,
                        principalTable: "Disciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvasDisciplinas_Provas_ProvaId",
                        column: x => x.ProvaId,
                        principalTable: "Provas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Provas_MatrizReferenciaId",
                table: "Provas",
                column: "MatrizReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvasDisciplinas_DisciplinaId",
                table: "ProvasDisciplinas",
                column: "DisciplinaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvasDisciplinas_ProvaId_DisciplinaId",
                table: "ProvasDisciplinas",
                columns: new[] { "ProvaId", "DisciplinaId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Provas_Disciplinas_DisciplinaId",
                table: "Provas",
                column: "DisciplinaId",
                principalTable: "Disciplinas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Provas_MatrizesReferencia_MatrizReferenciaId",
                table: "Provas",
                column: "MatrizReferenciaId",
                principalTable: "MatrizesReferencia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Provas_Disciplinas_DisciplinaId",
                table: "Provas");

            migrationBuilder.DropForeignKey(
                name: "FK_Provas_MatrizesReferencia_MatrizReferenciaId",
                table: "Provas");

            migrationBuilder.DropTable(
                name: "ProvasDisciplinas");

            migrationBuilder.DropIndex(
                name: "IX_Provas_MatrizReferenciaId",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "MatrizReferenciaId",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "TipoEscopo",
                table: "Provas");

            migrationBuilder.AlterColumn<int>(
                name: "DisciplinaId",
                table: "Provas",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Provas_Disciplinas_DisciplinaId",
                table: "Provas",
                column: "DisciplinaId",
                principalTable: "Disciplinas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
