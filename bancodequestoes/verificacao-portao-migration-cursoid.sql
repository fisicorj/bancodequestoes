-- Verificação isolada da LÓGICA do portão/backfill corrigido da migration
-- RemoveQuestaoCursoLegado (itens 32-34 da tarefa "Correção Final da
-- Normalização Acadêmica"). Como Questao.CursoId não existe mais no modelo
-- C#, não dá pra escrever isso como teste xUnit/EF — este script recria a
-- MESMA lógica de SQL (backfill idempotente + portão específico) contra
-- tabelas temporárias, dentro de uma transação sempre revertida no final.
-- Não lê nem escreve NADA nas tabelas reais (Questoes/Cursos/
-- QuestoesAreasCurso) — seguro rodar contra qualquer banco, inclusive o de
-- produção, mas o recomendado é rodar contra um banco de desenvolvimento/
-- teste mesmo assim.
--
-- Como rodar: cole o conteúdo inteiro num client psql/pgAdmin conectado ao
-- Postgres do projeto e execute de uma vez. Cada bloco "RAISE NOTICE"
-- imprime PASSOU/FALHOU — se algum "FALHOU" aparecer, a lógica do portão
-- tem um problema (não deveria acontecer, isto é só uma dupla-checagem).

BEGIN;

CREATE TEMP TABLE t_areas (id serial PRIMARY KEY, nome text);
CREATE TEMP TABLE t_cursos (id serial PRIMARY KEY, area_id int NULL);
CREATE TEMP TABLE t_questoes (id serial PRIMARY KEY, curso_id_legado int NULL);
CREATE TEMP TABLE t_questoes_areas (questao_id int, area_id int, PRIMARY KEY (questao_id, area_id));

-- Cenário 1 (item 32): Questao.CursoId legado aponta pra Curso de
-- "Engenharia de Computação", mas a questão JÁ tem QuestaoAreaCurso -> ADS
-- (de outro motivo qualquer). Após o backfill, tem que ficar com ADS +
-- Engenharia de Computação — as DUAS, nenhuma substituindo a outra.
INSERT INTO t_areas (nome) VALUES ('Engenharia de Computação'), ('ADS'); -- ids 1, 2
INSERT INTO t_cursos (area_id) VALUES (1); -- id 1, aponta pra Eng. Comp.
INSERT INTO t_questoes (curso_id_legado) VALUES (1); -- id 1
INSERT INTO t_questoes_areas VALUES (1, 2); -- já tem ADS (id 2)

-- Cenário 2 (item 33): questão já com o backfill certo feito antes — não
-- pode duplicar linha nem dar erro de chave primária ao rodar de novo.
INSERT INTO t_cursos (area_id) VALUES (1); -- id 2, também Eng. Comp.
INSERT INTO t_questoes (curso_id_legado) VALUES (2); -- id 2
INSERT INTO t_questoes_areas VALUES (2, 1); -- já tem Eng. Comp. (id 1) — backfill não deve duplicar

-- Cenário 3 (item 34): Curso sem AreaCurso definida — não há pra onde
-- migrar; o portão TEM que bloquear (não pode simplesmente derrubar a
-- coluna e perder a informação).
INSERT INTO t_cursos (area_id) VALUES (NULL); -- id 3, sem área
INSERT INTO t_questoes (curso_id_legado) VALUES (3); -- id 3

-- Backfill (mesma lógica da migration, contra as tabelas temp).
INSERT INTO t_questoes_areas (questao_id, area_id)
SELECT q.id, c.area_id
FROM t_questoes q
JOIN t_cursos c ON c.id = q.curso_id_legado
WHERE q.curso_id_legado IS NOT NULL AND c.area_id IS NOT NULL
ON CONFLICT (questao_id, area_id) DO NOTHING;

-- Checagem 1: questão 1 (cenário do enunciado) tem AGORA as duas áreas.
DO $$
DECLARE tem_ambas boolean;
BEGIN
    SELECT (COUNT(*) = 2) INTO tem_ambas
    FROM t_questoes_areas WHERE questao_id = 1 AND area_id IN (1, 2);
    IF tem_ambas THEN
        RAISE NOTICE 'Cenario 1 (backfill preserva associacao ja existente + adiciona a do CursoId): PASSOU';
    ELSE
        RAISE NOTICE 'Cenario 1 (backfill preserva associacao ja existente + adiciona a do CursoId): FALHOU';
    END IF;
END $$;

-- Checagem 2: questão 2 continua com exatamente 1 linha (sem duplicata).
DO $$
DECLARE total integer;
BEGIN
    SELECT COUNT(*) INTO total FROM t_questoes_areas WHERE questao_id = 2;
    IF total = 1 THEN
        RAISE NOTICE 'Cenario 2 (backfill nao duplica em dado ja migrado): PASSOU';
    ELSE
        RAISE NOTICE 'Cenario 2 (backfill nao duplica em dado ja migrado): FALHOU (total=%)', total;
    END IF;
END $$;

-- Portão corrigido (mesma lógica da migration): bloqueia se sobrar alguma
-- questão com curso_id_legado preenchido cujo Curso não tem área, OU sem a
-- QuestaoAreaCurso ESPECÍFICA correspondente.
DO $$
DECLARE pendentes integer;
BEGIN
    SELECT COUNT(*) INTO pendentes
    FROM t_questoes q
    LEFT JOIN t_cursos c ON c.id = q.curso_id_legado
    WHERE q.curso_id_legado IS NOT NULL
      AND (
          c.area_id IS NULL
          OR NOT EXISTS (
              SELECT 1 FROM t_questoes_areas qa
              WHERE qa.questao_id = q.id AND qa.area_id = c.area_id
          )
      );

    IF pendentes = 1 THEN
        RAISE NOTICE 'Cenario 3 (portao bloqueia Curso sem AreaCurso, nao perde dado silenciosamente): PASSOU (pendentes=%, esperado=1, e a questao 3)', pendentes;
    ELSE
        RAISE NOTICE 'Cenario 3 (portao bloqueia Curso sem AreaCurso, nao perde dado silenciosamente): FALHOU (pendentes=%, esperado=1)', pendentes;
    END IF;
END $$;

ROLLBACK;
