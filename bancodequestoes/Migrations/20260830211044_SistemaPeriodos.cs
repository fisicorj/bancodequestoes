using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class SistemaPeriodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bimestre",
                table: "Turmas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bimestre",
                table: "Provas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SistemaPeriodos",
                table: "Instituicoes",
                type: "text",
                nullable: false,
                defaultValue: "Semestral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bimestre",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "Bimestre",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "SistemaPeriodos",
                table: "Instituicoes");
        }
    }
}
