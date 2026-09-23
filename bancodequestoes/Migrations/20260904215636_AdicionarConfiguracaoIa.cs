using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConfiguracaoIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    Modo = table.Column<string>(type: "text", nullable: false),
                    BaseUrlLocal = table.Column<string>(type: "text", nullable: false),
                    ModeloLocal = table.Column<string>(type: "text", nullable: false),
                    ApiKeyNuvem = table.Column<string>(type: "text", nullable: true),
                    ModeloNuvem = table.Column<string>(type: "text", nullable: false),
                    TimeoutSegundos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesIa", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesIa");
        }
    }
}
