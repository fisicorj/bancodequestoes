using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class CartaoResposta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CartoesRespostaAplicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProvaId = table.Column<int>(type: "integer", nullable: false),
                    TurmaId = table.Column<int>(type: "integer", nullable: false),
                    CriadoPorId = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartoesRespostaAplicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartoesRespostaAplicacao_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CartoesRespostaAplicacao_Provas_ProvaId",
                        column: x => x.ProvaId,
                        principalTable: "Provas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartoesRespostaAplicacao_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartoesAlunoAplicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CartaoRespostaAplicacaoId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    NotaTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    CorrigidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartoesAlunoAplicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartoesAlunoAplicacao_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartoesAlunoAplicacao_CartoesRespostaAplicacao_CartaoRespos~",
                        column: x => x.CartaoRespostaAplicacaoId,
                        principalTable: "CartoesRespostaAplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespostasCartaoQuestao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CartaoAlunoAplicacaoId = table.Column<int>(type: "integer", nullable: false),
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    LetraDetectada = table.Column<char>(type: "character(1)", nullable: true),
                    Ambigua = table.Column<bool>(type: "boolean", nullable: false),
                    LetraConfirmada = table.Column<char>(type: "character(1)", nullable: true),
                    Correta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasCartaoQuestao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasCartaoQuestao_CartoesAlunoAplicacao_CartaoAlunoApl~",
                        column: x => x.CartaoAlunoAplicacaoId,
                        principalTable: "CartoesAlunoAplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespostasCartaoQuestao_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartoesAlunoAplicacao_AlunoId",
                table: "CartoesAlunoAplicacao",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_CartoesAlunoAplicacao_CartaoRespostaAplicacaoId_AlunoId",
                table: "CartoesAlunoAplicacao",
                columns: new[] { "CartaoRespostaAplicacaoId", "AlunoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartoesAlunoAplicacao_Token",
                table: "CartoesAlunoAplicacao",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartoesRespostaAplicacao_CriadoPorId",
                table: "CartoesRespostaAplicacao",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_CartoesRespostaAplicacao_ProvaId",
                table: "CartoesRespostaAplicacao",
                column: "ProvaId");

            migrationBuilder.CreateIndex(
                name: "IX_CartoesRespostaAplicacao_TurmaId",
                table: "CartoesRespostaAplicacao",
                column: "TurmaId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCartaoQuestao_CartaoAlunoAplicacaoId",
                table: "RespostasCartaoQuestao",
                column: "CartaoAlunoAplicacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCartaoQuestao_QuestaoId",
                table: "RespostasCartaoQuestao",
                column: "QuestaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespostasCartaoQuestao");

            migrationBuilder.DropTable(
                name: "CartoesAlunoAplicacao");

            migrationBuilder.DropTable(
                name: "CartoesRespostaAplicacao");
        }
    }
}
