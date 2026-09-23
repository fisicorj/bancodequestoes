-- Etapa 2 (auditoria de dados) da tarefa "eliminar Questao.CursoId".
-- Só leitura (SELECT) — nada aqui altera o banco. Rode contra o Postgres
-- real e cole o resultado de volta.

-- 1) Total de Questoes com CursoId legado preenchido.
SELECT COUNT(*) AS total_com_cursoid
FROM "Questoes"
WHERE "CursoId" IS NOT NULL;

-- 2) Dessas, quantas JÁ têm o vínculo equivalente em QuestoesAreasCurso
--    (via Curso.AreaCursoId) — ou seja, já foram migradas corretamente
--    pela migration NormalizacaoModeloAcademico.
SELECT COUNT(*) AS ja_migradas_para_areacurso
FROM "Questoes" q
JOIN "Cursos" c ON c."Id" = q."CursoId"
JOIN "QuestoesAreasCurso" qa ON qa."QuestaoId" = q."Id" AND qa."AreaCursoId" = c."AreaCursoId"
WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NOT NULL;

-- 3) Questoes com CursoId preenchido cujo Curso NÃO tem AreaCursoId
--    definido — inconsistência: não há pra onde migrar sem inventar
--    dado. Precisam de classificação manual (ou o Curso precisa ganhar
--    uma AreaCurso) antes da remoção definitiva.
SELECT COUNT(*) AS cursoid_sem_areacurso_no_curso
FROM "Questoes" q
JOIN "Cursos" c ON c."Id" = q."CursoId"
WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NULL;

-- 3b) As mesmas, com detalhe (id da questão, enunciado truncado, curso).
SELECT q."Id" AS questao_id, LEFT(q."Enunciado", 80) AS enunciado, q."CursoId", c."Nome" AS curso_nome
FROM "Questoes" q
JOIN "Cursos" c ON c."Id" = q."CursoId"
WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NULL
ORDER BY q."Id";

-- 4) Questoes cujo CursoId aponta pra um Curso que NÃO EXISTE MAIS —
--    não deveria acontecer (FK), mas confirmando integridade mesmo assim.
SELECT COUNT(*) AS cursoid_orfao
FROM "Questoes" q
LEFT JOIN "Cursos" c ON c."Id" = q."CursoId"
WHERE q."CursoId" IS NOT NULL AND c."Id" IS NULL;

-- 5) Questoes com CursoId preenchido, Curso COM AreaCursoId, mas que
--    AINDA NÃO têm o vínculo QuestoesAreasCurso correspondente — é o que
--    a migration de validação/backfill (Etapa 3) precisa varrer de novo
--    antes da remoção (idealmente já é zero, já que a migration anterior
--    fez esse backfill; um valor > 0 aqui indica dado inserido/alterado
--    DEPOIS da migration NormalizacaoModeloAcademico rodar).
SELECT COUNT(*) AS pendente_de_backfill
FROM "Questoes" q
JOIN "Cursos" c ON c."Id" = q."CursoId"
LEFT JOIN "QuestoesAreasCurso" qa ON qa."QuestaoId" = q."Id" AND qa."AreaCursoId" = c."AreaCursoId"
WHERE q."CursoId" IS NOT NULL AND c."AreaCursoId" IS NOT NULL AND qa."QuestaoId" IS NULL;

-- 6) Questoes que dependem 100% do fallback legado hoje — têm CursoId,
--    mas NENHUM vínculo em QuestoesAreasCurso (nem pra essa área, nem
--    pra qualquer outra). É o universo que ficaria "invisível" pro pool
--    de um simulado modo Curso se o fallback fosse removido sem migrar.
SELECT COUNT(*) AS dependem_so_do_fallback
FROM "Questoes" q
WHERE q."CursoId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "QuestoesAreasCurso" qa WHERE qa."QuestaoId" = q."Id");
