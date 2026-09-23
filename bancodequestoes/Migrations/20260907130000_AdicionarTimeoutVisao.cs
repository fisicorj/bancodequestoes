using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTimeoutVisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue (não só defaultValueSql): a linha singleton (Id=1) já
            // existe em bancos onde a IA foi configurada antes desta migration —
            // uma coluna NOT NULL sem default falharia pra essa linha existente.
            migrationBuilder.AddColumn<int>(
                name: "TimeoutVisaoSegundos",
                table: "ConfiguracoesIa",
                type: "integer",
                nullable: false,
                defaultValue: 300);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeoutVisaoSegundos",
                table: "ConfiguracoesIa");
        }
    }
}
