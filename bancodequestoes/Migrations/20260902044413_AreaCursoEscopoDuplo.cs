using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class AreaCursoEscopoDuplo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "AreaCursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AreaCursoId",
                table: "Cursos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AreasCurso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NomeNormalizado = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Nome\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreasCurso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatrizesReferencia_AreaCursoId",
                table: "MatrizesReferencia",
                column: "AreaCursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId");

            migrationBuilder.CreateIndex(
                name: "IX_AreasCurso_NomeNormalizado",
                table: "AreasCurso",
                column: "NomeNormalizado",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MatrizesReferencia_AreasCurso_AreaCursoId",
                table: "MatrizesReferencia",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // BACKFILL (escopo duplo — ver MatrizReferencia.cs): antes desta
            // migration, TODA MatrizReferencia (inclusive ENADE/DCN) usava
            // CursoId. A partir de agora, ENADE/DCN devem usar AreaCursoId
            // (documento nacional do MEC/INEP, não institucional — ver
            // AreaCurso.cs) e PPC/Institucional/Outro continuam em CursoId.
            // Não existe, nos dados pré-existentes, nenhum campo de "área
            // nacional" separado do nome do Curso — então a única fonte
            // disponível pra nomear a AreaCurso herdada é o próprio
            // Curso.Nome. Isso é uma aproximação deliberada, não perfeita:
            //   - Nomes de curso escritos de forma diferente entre
            //     instituições pro "mesmo" curso nacional (ex.: "Eng. da
            //     Computação" vs. "Engenharia de Computação") viram DUAS
            //     AreaCurso distintas — o índice único em NomeNormalizado só
            //     dedupa nomes IGUAIS (case-insensitive), não sinônimos.
            //   - Inversamente, se duas Cursos de instituições diferentes já
            //     tinham cada um sua PRÓPRIA matriz ENADE/DCN importada
            //     separadamente com o MESMO nome de curso, elas passam a
            //     apontar pra UMA ÚNICA AreaCurso — o que é o comportamento
            //     pretendido (dedup nacional), mas pode deixar mais de uma
            //     matriz com Status=Ativa na mesma AreaCurso (a regra de
            //     "1 ativa por escopo" é validada em código, não no banco —
            //     ver MatrizReferenciaService), exigindo revisão manual
            //     depois do "dotnet ef database update" se isso ocorrer.
            migrationBuilder.Sql(
                """
                INSERT INTO "AreasCurso" ("Nome", "Ativo", "CriadoEm")
                SELECT DISTINCT c."Nome", true, now()
                FROM "Cursos" c
                WHERE EXISTS (
                    SELECT 1 FROM "MatrizesReferencia" mr
                    WHERE mr."CursoId" = c."Id" AND mr."Tipo" IN ('ENADE', 'DCN')
                )
                ON CONFLICT ("NomeNormalizado") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Cursos" c
                SET "AreaCursoId" = ac."Id"
                FROM "AreasCurso" ac
                WHERE lower(c."Nome") = lower(ac."Nome")
                  AND EXISTS (
                      SELECT 1 FROM "MatrizesReferencia" mr
                      WHERE mr."CursoId" = c."Id" AND mr."Tipo" IN ('ENADE', 'DCN')
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE "MatrizesReferencia" mr
                SET "AreaCursoId" = c."AreaCursoId",
                    "CursoId" = NULL
                FROM "Cursos" c
                WHERE mr."CursoId" = c."Id"
                  AND mr."Tipo" IN ('ENADE', 'DCN')
                  AND c."AreaCursoId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos");

            migrationBuilder.DropForeignKey(
                name: "FK_MatrizesReferencia_AreasCurso_AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropTable(
                name: "AreasCurso");

            migrationBuilder.DropIndex(
                name: "IX_MatrizesReferencia_AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropIndex(
                name: "IX_Cursos_AreaCursoId",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "AreaCursoId",
                table: "MatrizesReferencia");

            migrationBuilder.DropColumn(
                name: "AreaCursoId",
                table: "Cursos");

            migrationBuilder.AlterColumn<int>(
                name: "CursoId",
                table: "MatrizesReferencia",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
