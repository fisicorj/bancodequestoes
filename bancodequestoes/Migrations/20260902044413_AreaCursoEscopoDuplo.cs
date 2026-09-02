using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AreaCursoEscopoDuplo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "AreaCursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AreaCursoId",
                table: "Cursos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AreasCurso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NomeNormalizado = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Nome\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreasCurso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_AreaCursoId",
                table: "MatrizesReferencia",
                column: "AreaCursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId");

            migrationBuilder.CreateIndex(
                name: "IX_AreasCurso_NomeNormalizado",
                table: "AreasCurso",
                column: "NomeNormalizado",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MatrizesReferencia_AreasCurso_AreaCursoId",
                table: "MatrizesReferencia",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos");

            migrationBuilder.DropForeignKey(
                name: "FK_MatrizesReferencia_AreasCurso_AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropTable(
                name: "AreasCurso");

            migrationBuilder.DropIndex(
                name: "IX_MatrizesReferencia_AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropIndex(
                name: "IX_Cursos_AreaCursoId",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropColumn(
                name: "AreaCursoId",
                table: "Cursos");

            migrationBuilder.AlterColumn<int>(
                name: "CursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
