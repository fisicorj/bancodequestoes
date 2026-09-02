using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class MetadadosQuestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ano",
                table: "Questoes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bloom",
                table: "Questoes",
                type: "text",
                nullable: true);

            // "Autoral" é o valor padrão do enum em C# (OrigemQuestao.Autoral = 0),
            // mas o gerador de migração do EF não converte esse default pra a
            // coluna convertida em string automaticamente — daria "" pra toda
            // questão já existente, que não corresponde a nenhum membro do enum.
            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "Questoes",
                type: "text",
                nullable: false,
                defaultValue: "Autoral");

            migrationBuilder.AddColumn<string>(
                name: "Referencia",
                table: "Questoes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesTags",
                columns: table => new
                {
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesTags", x => new { x.QuestaoId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_QuestoesTags_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestoesTags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestoesTags_TagsId",
                table: "QuestoesTags",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Nome",
                table: "Tags",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestoesTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropColumn(
                name: "Ano",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "Bloom",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "Referencia",
                table: "Questoes");
        }
    }
}
