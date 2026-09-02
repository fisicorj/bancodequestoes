using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class RecursosImagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alinhamento",
                table: "QuestoesImagens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LarguraPercentual",
                table: "QuestoesImagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Legenda",
                table: "QuestoesImagens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoAlternativo",
                table: "QuestoesImagens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alinhamento",
                table: "QuestoesImagens");

            migrationBuilder.DropColumn(
                name: "LarguraPercentual",
                table: "QuestoesImagens");

            migrationBuilder.DropColumn(
                name: "Legenda",
                table: "QuestoesImagens");

            migrationBuilder.DropColumn(
                name: "TextoAlternativo",
                table: "QuestoesImagens");
        }
    }
}
