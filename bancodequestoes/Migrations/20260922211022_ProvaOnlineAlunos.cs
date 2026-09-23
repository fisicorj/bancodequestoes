using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class ProvaOnlineAlunos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alunos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Matricula = table.Column<string>(type: "text", nullable: true),
                    InstituicaoId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alunos_Instituicoes_InstituicaoId",
                        column: x => x.InstituicaoId,
                        principalTable: "Instituicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AplicacoesProva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProvaId = table.Column<int>(type: "integer", nullable: false),
                    TurmaId = table.Column<int>(type: "integer", nullable: false),
                    CodigoAcesso = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DataLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TempoLimiteMinutos = table.Column<int>(type: "integer", nullable: true),
                    CriadoPorId = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AplicacoesProva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AplicacoesProva_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AplicacoesProva_Provas_ProvaId",
                        column: x => x.ProvaId,
                        principalTable: "Provas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AplicacoesProva_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurmasAlunos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurmaId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurmasAlunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurmasAlunos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurmasAlunos_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RespostasProvaOnline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoProvaId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IniciadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoEncerramento = table.Column<string>(type: "text", nullable: true),
                    NotaTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    LiberadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasProvaOnline", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasProvaOnline_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespostasProvaOnline_AplicacoesProva_AplicacaoProvaId",
                        column: x => x.AplicacaoProvaId,
                        principalTable: "AplicacoesProva",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RespostasQuestaoOnline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RespostaProvaOnlineId = table.Column<int>(type: "integer", nullable: false),
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    RespostaMultiplaEscolha = table.Column<char>(type: "character(1)", nullable: true),
                    RespostaCertoErrado = table.Column<bool>(type: "boolean", nullable: true),
                    RespostaNumerica = table.Column<decimal>(type: "numeric", nullable: true),
                    Correta = table.Column<bool>(type: "boolean", nullable: true),
                    PontuacaoObtida = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasQuestaoOnline", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasQuestaoOnline_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespostasQuestaoOnline_RespostasProvaOnline_RespostaProvaOn~",
                        column: x => x.RespostaProvaOnlineId,
                        principalTable: "RespostasProvaOnline",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespostasLacunaOnline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RespostaQuestaoOnlineId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    RespostaTexto = table.Column<string>(type: "text", nullable: false),
                    Correta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasLacunaOnline", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasLacunaOnline_RespostasQuestaoOnline_RespostaQuesta~",
                        column: x => x.RespostaQuestaoOnlineId,
                        principalTable: "RespostasQuestaoOnline",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_InstituicaoId",
                table: "Alunos",
                column: "InstituicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AplicacoesProva_CodigoAcesso",
                table: "AplicacoesProva",
                column: "CodigoAcesso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AplicacoesProva_CriadoPorId",
                table: "AplicacoesProva",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AplicacoesProva_ProvaId",
                table: "AplicacoesProva",
                column: "ProvaId");

            migrationBuilder.CreateIndex(
                name: "IX_AplicacoesProva_TurmaId",
                table: "AplicacoesProva",
                column: "TurmaId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasLacunaOnline_RespostaQuestaoOnlineId",
                table: "RespostasLacunaOnline",
                column: "RespostaQuestaoOnlineId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasProvaOnline_AlunoId",
                table: "RespostasProvaOnline",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasProvaOnline_AplicacaoProvaId_AlunoId",
                table: "RespostasProvaOnline",
                columns: new[] { "AplicacaoProvaId", "AlunoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RespostasQuestaoOnline_QuestaoId",
                table: "RespostasQuestaoOnline",
                column: "QuestaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasQuestaoOnline_RespostaProvaOnlineId",
                table: "RespostasQuestaoOnline",
                column: "RespostaProvaOnlineId");

            migrationBuilder.CreateIndex(
                name: "IX_TurmasAlunos_AlunoId",
                table: "TurmasAlunos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_TurmasAlunos_TurmaId_AlunoId",
                table: "TurmasAlunos",
                columns: new[] { "TurmaId", "AlunoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespostasLacunaOnline");

            migrationBuilder.DropTable(
                name: "TurmasAlunos");

            migrationBuilder.DropTable(
                name: "RespostasQuestaoOnline");

            migrationBuilder.DropTable(
                name: "RespostasProvaOnline");

            migrationBuilder.DropTable(
                name: "Alunos");

            migrationBuilder.DropTable(
                name: "AplicacoesProva");
        }
    }
}
