using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class MatrizReferenciaCurricular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestoesDiretrizes");

            migrationBuilder.DropTable(
                name: "DiretrizesCurriculares");

            migrationBuilder.CreateTable(
                name: "MatrizesReferencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CursoId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: true),
                    Edicao = table.Column<string>(type: "text", nullable: true),
                    Orgao = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    UrlFonte = table.Column<string>(type: "text", nullable: true),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatrizesReferencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizesReferencia_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensMatrizReferencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatrizReferenciaId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensMatrizReferencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensMatrizReferencia_MatrizesReferencia_MatrizReferenciaId",
                        column: x => x.MatrizReferenciaId,
                        principalTable: "MatrizesReferencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesItensMatriz",
                columns: table => new
                {
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    ItemMatrizReferenciaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesItensMatriz", x => new { x.QuestaoId, x.ItemMatrizReferenciaId });
                    table.ForeignKey(
                        name: "FK_QuestoesItensMatriz_ItensMatrizReferencia_ItemMatrizReferen~",
                        column: x => x.ItemMatrizReferenciaId,
                        principalTable: "ItensMatrizReferencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestoesItensMatriz_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensMatrizReferencia_Codigo",
                table: "ItensMatrizReferencia",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_ItensMatrizReferencia_MatrizReferenciaId",
                table: "ItensMatrizReferencia",
                column: "MatrizReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensMatrizReferencia_MatrizReferenciaId_Codigo",
                table: "ItensMatrizReferencia",
                columns: new[] { "MatrizReferenciaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensMatrizReferencia_Tipo",
                table: "ItensMatrizReferencia",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_Ano",
                table: "MatrizesReferencia",
                column: "Ano");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_CursoId",
                table: "MatrizesReferencia",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_Status",
                table: "MatrizesReferencia",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_Tipo",
                table: "MatrizesReferencia",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_QuestoesItensMatriz_ItemMatrizReferenciaId",
                table: "QuestoesItensMatriz",
                column: "ItemMatrizReferenciaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestoesItensMatriz");

            migrationBuilder.DropTable(
                name: "ItensMatrizReferencia");

            migrationBuilder.DropTable(
                name: "MatrizesReferencia");

            migrationBuilder.CreateTable(
                name: "DiretrizesCurriculares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CursoId = table.Column<int>(type: "integer", nullable: false),
                    DiretrizPaiId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiretrizesCurriculares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiretrizesCurriculares_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiretrizesCurriculares_DiretrizesCurriculares_DiretrizPaiId",
                        column: x => x.DiretrizPaiId,
                        principalTable: "DiretrizesCurriculares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesDiretrizes",
                columns: table => new
                {
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    DiretrizCurricularId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesDiretrizes", x => new { x.QuestaoId, x.DiretrizCurricularId });
                    table.ForeignKey(
                        name: "FK_QuestoesDiretrizes_DiretrizesCurriculares_DiretrizCurricula~",
                        column: x => x.DiretrizCurricularId,
                        principalTable: "DiretrizesCurriculares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestoesDiretrizes_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_CursoId",
                table: "DiretrizesCurriculares",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_DiretrizPaiId",
                table: "DiretrizesCurriculares",
                column: "DiretrizPaiId");

            migrationBuilder.CreateIndex(
                name: "IX_DiretrizesCurriculares_Tipo",
                table: "DiretrizesCurriculares",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_QuestoesDiretrizes_DiretrizCurricularId",
                table: "QuestoesDiretrizes",
                column: "DiretrizCurricularId");
        }
    }
}
