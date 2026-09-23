using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "Correção Final da Normalização Acadêmica — Banco de
// Questões" (itens 32-48 do pedido). Cobrem a separação rigorosa entre
// matriz NACIONAL (ENADE/DCN -> AreaCurso, validada só por
// QuestaoAreaCurso) e matriz INSTITUCIONAL (PPC/Institucional/Outro ->
// Curso, validada só por CursoContextoMatrizId), e as invariantes que
// QuestaoCurricularService.ResolverItensMatrizAsync agora impõe por REJEIÇÃO
// explícita (nunca por filtro silencioso): item nacional incompatível com
// as Áreas marcadas, e item institucional de mais de um Curso ao mesmo
// tempo.
//
// Itens 32-34 do pedido (backfill/gate da migration RemoveQuestaoCursoLegado
// contra dado legado) NÃO são expressáveis aqui: Questao.CursoId não existe
// mais no modelo C#, então não há como seedar esse cenário via EF/InMemory
// — só via SQL direto contra um Postgres real (ver
// verificacao-portao-migration-cursoid.sql, script à parte, autocontido e
// que roda dentro de uma transação sempre revertida — não toca dado real).
public class CorrecaoFinalNormalizacaoAcademicaTests
{
    private static QuestaoService NovoService(BancoQuestoes.Data.ApplicationDbContext db) =>
        TestFakes.NovoQuestaoService(db);

    // Item 35 — ENADE compatível: questão já marcada pra "Engenharia de
    // Computação" aceita item ENADE dessa mesma área.
    [Fact]
    public async Task ResolverItens_ItemEnadeCompativelComAreaMarcada_EhAceito()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matrizNacional = new MatrizReferencia { Nome = "ENADE 2023", AreaCursoId = area.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.Add(matrizNacional);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C08", "Competência 8", matrizNacional.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int> { area.Id },
            ItemMatrizIds = new List<int> { item.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
        Assert.Equal(item.Id, recarregada.ItensMatriz[0].Id);
    }

    // Item 36 — ENADE INCOMPATÍVEL (o teste mais importante da tarefa):
    // questão marcada só pra "ADS" NÃO pode receber item ENADE de
    // "Engenharia de Computação", mesmo que o professor tenha escolhido um
    // Curso de Engenharia de Computação como CursoContextoMatrizId. O Curso
    // institucional NUNCA concede acesso implícito a matriz nacional.
    [Fact]
    public async Task ResolverItens_ItemEnadeIncompativelComArea_EhRejeitadoMesmoComCursoContextoDaAreaCerta()
    {
        using var db = TestDbFactory.Criar();

        var areaAds = TestSeed.AreaCurso("ADS");
        var areaEngComp = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(areaAds, areaEngComp);
        await db.SaveChangesAsync();

        var cursoEngComp = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1, areaCursoId: areaEngComp.Id);
        db.Cursos.Add(cursoEngComp);
        await db.SaveChangesAsync();

        var matrizEnade = new MatrizReferencia { Nome = "ENADE 2023", AreaCursoId = areaEngComp.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.Add(matrizEnade);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C08", "Competência 8", matrizEnade.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int> { areaAds.Id }, // só ADS marcada
            CursoContextoMatrizId = cursoEngComp.Id, // curso de Eng. Comp. escolhido, mas isso não basta
            ItemMatrizIds = new List<int> { item.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    // Item 37 — PPC correto: CursoContextoMatrizId = Curso A aceita item PPC
    // do próprio Curso A.
    [Fact]
    public async Task ResolverItens_ItemPpcDoCursoContexto_EhAceito()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            CursoContextoMatrizId = cursoA.Id,
            ItemMatrizIds = new List<int> { item.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);
        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
    }

    // Item 38 — PPC de OUTRO curso: CursoContextoMatrizId = Curso A não pode
    // aceitar item PPC do Curso B — rejeitado explicitamente (não filtrado
    // em silêncio), porque o item pedido não bate com o contexto declarado.
    [Fact]
    public async Task ResolverItens_ItemPpcDeOutroCurso_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        var cursoB = TestSeed.Curso("ADS", instituicaoId: 1);
        db.Cursos.AddRange(cursoA, cursoB);
        await db.SaveChangesAsync();

        var ppcB = TestSeed.Matriz("PPC do B", cursoB.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppcB);
        await db.SaveChangesAsync();
        var itemB = TestSeed.Item("C01", "Competência 1 do B", ppcB.Id);
        db.ItensMatrizReferencia.Add(itemB);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            CursoContextoMatrizId = cursoA.Id,
            ItemMatrizIds = new List<int> { itemB.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    // Item 39 — dois PPCs ao mesmo tempo: item do Curso A + item do Curso B
    // juntos deve falhar nesta versão (uma questão só tem UM contexto
    // institucional por vez).
    [Fact]
    public async Task ResolverItens_ItensPpcDeDoisCursosJuntos_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        var cursoB = TestSeed.Curso("ADS", instituicaoId: 1);
        db.Cursos.AddRange(cursoA, cursoB);
        await db.SaveChangesAsync();

        var ppcA = TestSeed.Matriz("PPC do A", cursoA.Id, TipoMatrizReferencia.PPC);
        var ppcB = TestSeed.Matriz("PPC do B", cursoB.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.AddRange(ppcA, ppcB);
        await db.SaveChangesAsync();
        var itemA = TestSeed.Item("C01", "Competência 1 do A", ppcA.Id);
        var itemB = TestSeed.Item("C01", "Competência 1 do B", ppcB.Id);
        db.ItensMatrizReferencia.AddRange(itemA, itemB);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            CursoContextoMatrizId = cursoA.Id,
            ItemMatrizIds = new List<int> { itemA.Id, itemB.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    // Item 40 — matriz nacional NÃO depende de contexto institucional:
    // questão com AreaCurso marcada, SEM nenhum CursoContextoMatrizId,
    // consegue usar item ENADE da área — prova que ENADE/DCN são
    // acadêmicos, não institucionais.
    [Fact]
    public async Task ResolverItens_ItemEnadeSemCursoContexto_EhAceito()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matrizNacional = new MatrizReferencia { Nome = "ENADE 2023", AreaCursoId = area.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.Add(matrizNacional);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C08", "Competência 8", matrizNacional.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int> { area.Id },
            CursoContextoMatrizId = null,
            ItemMatrizIds = new List<int> { item.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);
        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
    }

    // Item 42 — PPC NÃO compartilhado: PPC do Curso A não vale como matriz
    // institucional do Curso B, mesmo que os dois representem a MESMA
    // AreaCurso — institucional é sempre por Curso específico, nunca por
    // Área (diferente de ENADE/DCN).
    [Fact]
    public async Task ValidarItensDoCurso_PpcDeCursoComMesmaArea_NaoEhAceitoPeloOutroCurso()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var cursoA = TestSeed.Curso("Eng. Comp. — Instituição A", instituicaoId: 1, areaCursoId: area.Id);
        var cursoB = TestSeed.Curso("Eng. Comp. — Instituição B", instituicaoId: 2, areaCursoId: area.Id);
        db.Cursos.AddRange(cursoA, cursoB);
        await db.SaveChangesAsync();

        var ppcA = TestSeed.Matriz("PPC da Instituição A", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppcA);
        await db.SaveChangesAsync();
        var itemA = TestSeed.Item("C01", "Competência 1", ppcA.Id);
        db.ItensMatrizReferencia.Add(itemA);
        await db.SaveChangesAsync();

        var matrizService = TestFakes.NovaMatrizReferenciaService(db);
        var aceitosPeloB = await matrizService.ValidarItensDoCursoAsync(cursoB.Id, new List<int> { itemA.Id });

        Assert.Empty(aceitosPeloB);
    }

    // Item 48 — questão multiárea: ADS + Engenharia de Computação podem usar
    // matrizes nacionais compatíveis com QUALQUER uma das duas, sem
    // duplicar a questão (continua sendo uma linha só em Questoes).
    [Fact]
    public async Task ResolverItens_QuestaoMultiarea_AceitaItensDasDuasAreas()
    {
        using var db = TestDbFactory.Criar();

        var areaAds = TestSeed.AreaCurso("ADS");
        var areaEngComp = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(areaAds, areaEngComp);
        await db.SaveChangesAsync();

        var matrizAds = new MatrizReferencia { Nome = "ENADE ADS", AreaCursoId = areaAds.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        var matrizEngComp = new MatrizReferencia { Nome = "ENADE Eng. Comp.", AreaCursoId = areaEngComp.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.AddRange(matrizAds, matrizEngComp);
        await db.SaveChangesAsync();
        var itemAds = TestSeed.Item("A01", "Item ADS", matrizAds.Id);
        var itemEngComp = TestSeed.Item("C08", "Item Eng. Comp.", matrizEngComp.Id);
        db.ItensMatrizReferencia.AddRange(itemAds, itemEngComp);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int> { areaAds.Id, areaEngComp.Id },
            ItemMatrizIds = new List<int> { itemAds.Id, itemEngComp.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(1, await db.Questoes.CountAsync(q => q.Id == criada.Id));
        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        await db.Entry(recarregada!).Collection(q => q.AreasCurso).LoadAsync();
        Assert.Equal(2, recarregada!.ItensMatriz.Count);
        Assert.Equal(2, recarregada.AreasCurso.Count);
    }

    // Item 63/64 — remover a Área não pode deixar o item nacional
    // "pendurado": ao editar, se o item nacional continuar marcado mas a
    // Área correspondente for desmarcada, a edição é rejeitada (bloqueio
    // explícito, nunca remoção silenciosa do item).
    [Fact]
    public async Task AtualizarAsync_RemoverAreaMasManterItemNacionalDela_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matrizNacional = new MatrizReferencia { Nome = "ENADE 2023", AreaCursoId = area.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.Add(matrizNacional);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C08", "Competência 8", matrizNacional.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var criarModelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int> { area.Id },
            ItemMatrizIds = new List<int> { item.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // Edição: desmarca a Área, mas o item nacional continua na lista
        // enviada (ex.: bug de tela, ou POST forjado) — precisa ser
        // rejeitado, não aceito com o item silenciosamente removido.
        var editarModelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Questão de teste",
            AreaCursoIds = new List<int>(), // Área removida
            ItemMatrizIds = new List<int> { item.Id }, // item nacional continua marcado
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.AtualizarAsync(
                criada.Id,
                editarModelo,
                new List<QuestaoImagem>(),
                new HashSet<int>(),
                new List<PendenteImagem>(),
                meuId: "prof-1",
                minhaInstituicaoId: null));
    }
}
