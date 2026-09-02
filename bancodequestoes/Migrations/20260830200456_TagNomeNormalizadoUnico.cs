using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class TagNomeNormalizadoUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_Nome",
                table: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "NomeNormalizado",
                table: "Tags",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(\"Nome\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NomeNormalizado",
                table: "Tags",
                column: "NomeNormalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_NomeNormalizado",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "NomeNormalizado",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Nome",
                table: "Tags",
                column: "Nome",
                unique: true);
        }
    }
}
