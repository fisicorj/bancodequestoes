using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class NormalizacaoModeloAcademico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos");

            migrationBuilder.CreateTable(
                name: "CursosDisciplinas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CursoId = table.Column<int>(type: "integer", nullable: false),
                    DisciplinaId = table.Column<int>(type: "integer", nullable: false),
                    Semestre = table.Column<int>(type: "integer", nullable: true),
                    CargaHoraria = table.Column<int>(type: "integer", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CursosDisciplinas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CursosDisciplinas_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CursosDisciplinas_Disciplinas_DisciplinaId",
                        column: x => x.DisciplinaId,
                        principalTable: "Disciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestoesAreasCurso",
                columns: table => new
                {
                    QuestaoId = table.Column<int>(type: "integer", nullable: false),
                    AreaCursoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestoesAreasCurso", x => new { x.QuestaoId, x.AreaCursoId });
                    table.ForeignKey(
                        name: "FK_QuestoesAreasCurso_AreasCurso_AreaCursoId",
                        column: x => x.AreaCursoId,
                        principalTable: "AreasCurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestoesAreasCurso_Questoes_QuestaoId",
                        column: x => x.QuestaoId,
                        principalTable: "Questoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CursosDisciplinas_CursoId_DisciplinaId",
                table: "CursosDisciplinas",
                columns: new[] { "CursoId", "DisciplinaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CursosDisciplinas_DisciplinaId",
                table: "CursosDisciplinas",
                column: "DisciplinaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestoesAreasCurso_AreaCursoId",
                table: "QuestoesAreasCurso",
                column: "AreaCursoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // BACKFILL 1/2 — CursosDisciplinas (ver models/CursoDisciplina.cs):
            // antes desta migration, a única fonte de "esta Disciplina está na
            // grade deste Curso" era inferir via Turma. Aqui, cada combinação
            // DISTINTA de Turma.CursoId+DisciplinaId já existente vira uma
            // linha de CursoDisciplina — não perde nenhuma relação que já
            // valia implicitamente, só torna explícito o que já era verdade.
            // Semestre/CargaHoraria ficam NULL (não existe fonte pra eles nos
            // dados de Turma) — o professor preenche depois, se quiser, na
            // tela de gestão; o vínculo em si (a linha existir) já é o que
            // importa pra Turma poder validar contra CursoDisciplina.
            // ON CONFLICT usa o índice único (CursoId, DisciplinaId) —
            // idempotente, seguro reexecutar.
            migrationBuilder.Sql(
                """
                INSERT INTO "CursosDisciplinas" ("CursoId", "DisciplinaId", "Ativa")
                SELECT DISTINCT t."CursoId", t."DisciplinaId", true
                FROM "Turmas" t
                ON CONFLICT ("CursoId", "DisciplinaId") DO NOTHING;
                """);

            // BACKFILL 2/2 — QuestoesAreasCurso (ver models/QuestaoAreaCurso.cs
            // e o comentário LEGADO em Questao.CursoId): pra toda Questao com
            // CursoId preenchido CUJO Curso já aponta pra uma AreaCurso, cria
            // o vínculo QuestaoAreaCurso correspondente — é isso que faz uma
            // questão hoje só "achável" pelo simulado de UMA instituição
            // passar a ser encontrada por QUALQUER instituição que compartilhe
            // a mesma AreaCurso nacional, sem duplicar a questão nem alterar
            // Questao.CursoId (mantido por compatibilidade, ver comentário no
            // model).
            //
            // Deliberadamente NÃO INVENTAMOS AreaCurso pra questão nenhuma:
            // uma Questao com CursoId preenchido mas cujo Curso NÃO tem
            // AreaCursoId definido simplesmente não gera linha aqui — fica
            // sem QuestaoAreaCurso até alguém definir a AreaCurso do Curso
            // (ou classificar a questão manualmente depois), reportado (sem
            // bloquear a migration) no bloco de diagnóstico logo abaixo.
            // ON CONFLICT usa a chave composta (QuestaoId, AreaCursoId) —
            // idempotente.
            migrationBuilder.Sql(
                """
                INSERT INTO "QuestoesAreasCurso" ("QuestaoId", "AreaCursoId")
                SELECT DISTINCT q."Id", c."AreaCursoId"
                FROM "Questoes" q
                JOIN "Cursos" c ON c."Id" = q."CursoId"
                WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NOT NULL
                ON CONFLICT ("QuestaoId", "AreaCursoId") DO NOTHING;
                """);

            // DIAGNÓSTICO DE DADOS LEGADOS (não bloqueia a migration — só
            // reporta via RAISE NOTICE, visível no console de quem rodar
            // "dotnet ef database update"). Três casos que o pedido de
            // normalização pediu explicitamente pra reportar, não corrigir
            // automaticamente:
            //   1) Questões com CursoId preenchido mas cujo Curso não tem
            //      AreaCursoId — não migraram pra QuestaoAreaCurso acima.
            //   2) Cursos sem AreaCurso definido (a causa-raiz do caso 1).
            //   3) MatrizReferencia nacional (ENADE/DCN) ainda presa a um
            //      CursoId em vez de AreaCursoId — não deveria mais existir
            //      depois da migration AreaCursoEscopoDuplo (que já migrou
            //      esses casos), mas fica aqui como rede de segurança caso
            //      alguma matriz nacional tenha sido criada/importada num
            //      Curso sem AreaCurso definido naquele momento.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    questoes_sem_area integer;
                    cursos_sem_area integer;
                    matrizes_nacionais_em_curso integer;
                BEGIN
                    SELECT COUNT(*) INTO questoes_sem_area
                    FROM "Questoes" q
                    JOIN "Cursos" c ON c."Id" = q."CursoId"
                    WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NULL;

                    SELECT COUNT(*) INTO cursos_sem_area
                    FROM "Cursos"
                    WHERE "AreaCursoId" IS NULL;

                    SELECT COUNT(*) INTO matrizes_nacionais_em_curso
                    FROM "MatrizesReferencia"
                    WHERE "Tipo" IN ('ENADE', 'DCN') AND "CursoId" IS NOT NULL;

                    RAISE NOTICE '[NormalizacaoModeloAcademico] % questão(ões) com CursoId preenchido cujo Curso não tem AreaCurso definido — não migraram para QuestaoAreaCurso (defina a AreaCurso do Curso, ou classifique a questão manualmente depois).', questoes_sem_area;
                    RAISE NOTICE '[NormalizacaoModeloAcademico] % curso(s) sem AreaCurso definido.', cursos_sem_area;
                    RAISE NOTICE '[NormalizacaoModeloAcademico] % matriz(es) de escopo nacional (ENADE/DCN) ainda presa(s) a um Curso institucional em vez de uma AreaCurso — revisão manual recomendada.', matrizes_nacionais_em_curso;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos");

            migrationBuilder.DropTable(
                name: "CursosDisciplinas");

            migrationBuilder.DropTable(
                name: "QuestoesAreasCurso");

            migrationBuilder.AddForeignKey(
                name: "FK_Cursos_AreasCurso_AreaCursoId",
                table: "Cursos",
                column: "AreaCursoId",
                principalTable: "AreasCurso",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
