using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class SuporteEnadeFormacaoGeral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoProvaOrigem",
                table: "Questoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroOriginal",
                table: "Questoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecaoEnade",
                table: "Questoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Disciplinas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoProvaOrigem",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "NumeroOriginal",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "SecaoEnade",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Disciplinas");
        }
    }
}
