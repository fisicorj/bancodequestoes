using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class ImagensNoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Path",
                table: "QuestoesImagens",
                newName: "NomeArquivo");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "QuestoesImagens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "Conteudo",
                table: "QuestoesImagens",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "QuestoesImagens");

            migrationBuilder.DropColumn(
                name: "Conteudo",
                table: "QuestoesImagens");

            migrationBuilder.RenameColumn(
                name: "NomeArquivo",
                table: "QuestoesImagens",
                newName: "Path");
        }
    }
}
