using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarModeloVisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModeloVisaoLocal",
                table: "ConfiguracoesIa",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeloVisaoNuvem",
                table: "ConfiguracoesIa",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeloVisaoLocal",
                table: "ConfiguracoesIa");

            migrationBuilder.DropColumn(
                name: "ModeloVisaoNuvem",
                table: "ConfiguracoesIa");
        }
    }
}
