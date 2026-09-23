using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQuestaoCursoLegado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tarefa "eliminar Questao.CursoId" (etapa 10/11) — antes de
            // remover a coluna de verdade, duas camadas de segurança, nessa
            // ordem, DENTRO da mesma transação da migration (Up() inteiro é
            // uma transação só, no Npgsql — se a segunda camada abortar, o
            // DROP COLUMN no final nunca chega a rodar):
            //
            // 1) Backfill de segurança (idempotente, ON CONFLICT DO NOTHING):
            //    reaplica a MESMA lógica da migration NormalizacaoModeloAcademico
            //    — cobre o caso de ter entrado dado novo com CursoId legado
            //    entre a auditoria manual e a aplicação desta migration.
            migrationBuilder.Sql(
                """
                INSERT INTO "QuestoesAreasCurso" ("QuestaoId", "AreaCursoId")
                SELECT q."Id", c."AreaCursoId"
                FROM "Questoes" q
                JOIN "Cursos" c ON c."Id" = q."CursoId"
                WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NOT NULL
                ON CONFLICT ("QuestaoId", "AreaCursoId") DO NOTHING;
                """);

            // 2) Portão de validação (bloqueante, não só um aviso) — CORRIGIDO
            //    na "Correção Final da Normalização Acadêmica" (item 3-9):
            //    o portão original só checava "a questão tem ALGUMA
            //    QuestaoAreaCurso" — insuficiente. Cenário que esse portão
            //    deixava passar: Questao.CursoId aponta pra um Curso de
            //    Engenharia de Computação, mas a questão só tem
            //    QuestaoAreaCurso -> ADS (marcada por outro motivo,
            //    manualmente) — "tem alguma área" era verdade, mas a área
            //    ESPECÍFICA que o CursoId legado representava não estava
            //    preservada, e a coluna seria removida mesmo assim.
            //
            //    Portão correto: bloqueia quando existe Questao.CursoId
            //    preenchido E (o Curso não tem AreaCursoId definida — não há
            //    pra onde migrar, ver item 8 do pedido original — OU não
            //    existe QuestaoAreaCurso com EXATAMENTE aquele
            //    QuestaoId+AreaCursoId, não só "alguma área qualquer"). Como
            //    o DropForeignKey ainda não rodou neste ponto, a FK garante
            //    que "Curso inexistente" nunca acontece aqui (LEFT JOIN só
            //    por defesa). RAISE EXCEPTION aborta a migration inteira
            //    (dentro da mesma transação) em vez de silenciosamente
            //    derrubar a coluna e a informação junto — "detectar e
            //    parar" em vez de "ser esperto demais".
            //
            //    Nota sobre já ter sido aplicada: esta migration já rodou
            //    contra o banco de desenvolvimento em 2026-09-02 (com a
            //    versão ANTERIOR, menos rigorosa, deste portão) — editar o
            //    texto aqui não re-executa nada nesse banco (EF Core nunca
            //    reaplica uma migration já registrada em
            //    __EFMigrationsHistory) nem muda o schema final produzido
            //    (o DROP COLUMN continua idêntico); só deixa o portão
            //    correto pronto pra qualquer outro banco (clone novo, CI,
            //    restauração de backup) que ainda vá aplicar esta migration.
            //    A auditoria manual de 2026-09-02 (auditoria-dados-questao-
            //    cursoid.sql, consulta 2) já tinha checado a condição
            //    ESPECÍFICA pra esse banco — 1 de 1 questão com CursoId já
            //    batia exatamente com a Área do seu Curso — então não há
            //    dado a corrigir retroativamente, só o texto da migration.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    pendentes integer;
                BEGIN
                    SELECT COUNT(*) INTO pendentes
                    FROM "Questoes" q
                    LEFT JOIN "Cursos" c ON c."Id" = q."CursoId"
                    WHERE q."CursoId" IS NOT NULL
                      AND (
                          c."AreaCursoId" IS NULL
                          OR NOT EXISTS (
                              SELECT 1 FROM "QuestoesAreasCurso" qa
                              WHERE qa."QuestaoId" = q."Id" AND qa."AreaCursoId" = c."AreaCursoId"
                          )
                      );

                    IF pendentes > 0 THEN
                        RAISE EXCEPTION 'RemoveQuestaoCursoLegado abortada: % questao(oes) com CursoId legado sem a QuestaoAreaCurso ESPECIFICA correspondente ao Curso.AreaCursoId (ou o Curso nao tem AreaCursoId definida). Classifique manualmente essas questoes (adicione a Area de Curso correta pela tela de edicao) antes de rodar esta migration de novo. Use auditoria-dados-questao-cursoid.sql (consultas 2 e 3) para listar os ids afetados.', pendentes;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Questoes_Cursos_CursoId",
                table: "Questoes");

            migrationBuilder.DropIndex(
                name: "IX_Questoes_CursoId",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "CursoId",
                table: "Questoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CursoId",
                table: "Questoes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questoes_CursoId",
                table: "Questoes",
                column: "CursoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questoes_Cursos_CursoId",
                table: "Questoes",
                column: "CursoId",
                principalTable: "Cursos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
