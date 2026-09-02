using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class NovosTiposQuestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestoesAssociacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesAssociacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestoesAssociacao_Questoes_Id",
                        column: x => x.Id,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesLacunas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesLacunas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestoesLacunas_Questoes_Id",
                        column: x => x.Id,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesNumericas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RespostaEsperada = table.Column<decimal>(type: "numeric", nullable: false),
                    Tolerancia = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesNumericas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestoesNumericas_Questoes_Id",
                        column: x => x.Id,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesRespostaBreve",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RespostaEsperada = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesRespostaBreve", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestoesRespostaBreve_Questoes_Id",
                        column: x => x.Id,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParesAssociacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestaoAssociacaoId = table.Column<int>(type: "integer", nullable: false),
                    Termo = table.Column<string>(type: "text", nullable: false),
                    Correspondente = table.Column<string>(type: "text", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParesAssociacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParesAssociacao_QuestoesAssociacao_QuestaoAssociacaoId",
                        column: x => x.QuestaoAssociacaoId,
                        principalTable: "QuestoesAssociacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LacunasRespostas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestaoLacunasId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    RespostaEsperada = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LacunasRespostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LacunasRespostas_QuestoesLacunas_QuestaoLacunasId",
                        column: x => x.QuestaoLacunasId,
                        principalTable: "QuestoesLacunas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LacunasRespostas_QuestaoLacunasId",
                table: "LacunasRespostas",
                column: "QuestaoLacunasId");

            migrationBuilder.CreateIndex(
                name: "IX_ParesAssociacao_QuestaoAssociacaoId",
                table: "ParesAssociacao",
                column: "QuestaoAssociacaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LacunasRespostas");

            migrationBuilder.DropTable(
                name: "ParesAssociacao");

            migrationBuilder.DropTable(
                name: "QuestoesNumericas");

            migrationBuilder.DropTable(
                name: "QuestoesRespostaBreve");

            migrationBuilder.DropTable(
                name: "QuestoesLacunas");

            migrationBuilder.DropTable(
                name: "QuestoesAssociacao");
        }
    }
}
