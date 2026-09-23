using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes de QuestaoQueryService contra InMemory: o pool dos três Escopos
// (Disciplina/Multidisciplinar/Curso) e a validação de IDOR de QuestaoId.
public class QuestaoQueryServiceEscopoTests
{
    [Fact]
    public async Task ObterCandidatasAsync_Disciplina_RetornaSoDaquelaDisciplina()
    {
        using var db = TestDbFactory.Criar();

        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo);
        await db.SaveChangesAsync();

        db.Questoes.AddRange(
            TestSeed.Questao("Q Redes 1", assuntoRedes.Id),
            TestSeed.Questao("Q Redes 2", assuntoRedes.Id),
            TestSeed.Questao("Q SO 1", assuntoSo.Id));
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var escopo = new EscopoQuestoes { DisciplinaIds = new HashSet<int> { redes.Id } };

        var (assuntos, questoes) = await query.ObterCandidatasAsync(escopo, meuId: null, minhaInstituicaoId: null);

        Assert.Equal(2, questoes.Count);
        Assert.All(questoes, q => Assert.Equal(assuntoRedes.Id, q.AssuntoId));
        Assert.Single(assuntos);
    }

    // Pool Multidisciplinar = união das disciplinas selecionadas; uma
    // terceira disciplina fora do conjunto nunca vaza pro pool.
    [Fact]
    public async Task ObterCandidatasAsync_Multidisciplinar_RetornaUniaoDasDisciplinasSelecionadas()
    {
        using var db = TestDbFactory.Criar();

        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        var arquitetura = TestSeed.Disciplina("Arquitetura de Computadores");
        db.Disciplinas.AddRange(redes, so, arquitetura);
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        var assuntoArquitetura = TestSeed.Assunto("Pipeline", arquitetura.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo, assuntoArquitetura);
        await db.SaveChangesAsync();

        db.Questoes.AddRange(
            TestSeed.Questao("Q Redes 1", assuntoRedes.Id),
            TestSeed.Questao("Q SO 1", assuntoSo.Id),
            TestSeed.Questao("Q SO 2", assuntoSo.Id),
            TestSeed.Questao("Q Arquitetura 1", assuntoArquitetura.Id)); // fora do escopo
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var escopo = new EscopoQuestoes { DisciplinaIds = new HashSet<int> { redes.Id, so.Id } };

        var (_, questoes) = await query.ObterCandidatasAsync(escopo, meuId: null, minhaInstituicaoId: null);

        Assert.Equal(3, questoes.Count);
        Assert.DoesNotContain(questoes, q => q.AssuntoId == assuntoArquitetura.Id);
    }

    // Modo Curso filtra por QuestaoAreaCurso (AreaCurso do Curso), nunca por
    // Disciplina; questão de outra área, ou sem vínculo, não aparece.
    [Fact]
    public async Task ObterCandidatasAsync_Curso_FiltraPorAreaCurso_NaoPorDisciplina()
    {
        using var db = TestDbFactory.Criar();

        var areaEngComp = TestSeed.AreaCurso("Engenharia de Computação");
        var areaAds = TestSeed.AreaCurso("Análise e Desenvolvimento de Sistemas");
        db.AreasCurso.AddRange(areaEngComp, areaAds);
        await db.SaveChangesAsync();

        var cursoEngComp = TestSeed.Curso("Engenharia de Computação", areaCursoId: areaEngComp.Id);
        var cursoAds = TestSeed.Curso("Análise e Desenvolvimento de Sistemas", areaCursoId: areaAds.Id);
        db.Cursos.AddRange(cursoEngComp, cursoAds);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assuntoRedes);
        await db.SaveChangesAsync();

        var doCurso = TestSeed.Questao("Da área certa", assuntoRedes.Id);
        doCurso.AreasCurso.Add(areaEngComp);
        var deOutroCurso = TestSeed.Questao("Da área de outro curso, mesma disciplina", assuntoRedes.Id);
        deOutroCurso.AreasCurso.Add(areaAds);
        var semVinculo = TestSeed.Questao("Sem vínculo de área", assuntoRedes.Id);
        db.Questoes.AddRange(doCurso, deOutroCurso, semVinculo);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var escopo = new EscopoQuestoes { CursoId = cursoEngComp.Id };

        var (_, questoes) = await query.ObterCandidatasAsync(escopo, meuId: null, minhaInstituicaoId: null);

        Assert.Single(questoes);
        Assert.Equal(doCurso.Id, questoes[0].Id);
    }

    // Uma Matriz nunca traz questões de outra matriz, mesmo compartilhando a
    // mesma AreaCurso.
    [Fact]
    public async Task ObterCandidatasAsync_ComMatrizReferenciaId_ExcluiQuestoesDeOutraMatriz()
    {
        using var db = TestDbFactory.Criar();

        var areaEngComp = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(areaEngComp);
        await db.SaveChangesAsync();

        var cursoEngComp = TestSeed.Curso("Engenharia de Computação", areaCursoId: areaEngComp.Id);
        db.Cursos.Add(cursoEngComp);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assuntoRedes);
        await db.SaveChangesAsync();

        var matrizEngComp = TestSeed.Matriz("ENADE 2023 — Eng. Computação", cursoEngComp.Id);
        var matrizAds = TestSeed.Matriz("ENADE 2021 — ADS", cursoEngComp.Id); // cadastrada no mesmo curso por engano
        db.MatrizesReferencia.AddRange(matrizEngComp, matrizAds);
        await db.SaveChangesAsync();

        var itemEngComp = TestSeed.Item("C07", "Competência 7", matrizEngComp.Id);
        var itemAds = TestSeed.Item("X01", "Item ADS", matrizAds.Id);
        db.ItensMatrizReferencia.AddRange(itemEngComp, itemAds);
        await db.SaveChangesAsync();

        var questaoEngComp = TestSeed.Questao("Questão da matriz certa", assuntoRedes.Id);
        questaoEngComp.AreasCurso.Add(areaEngComp);
        questaoEngComp.ItensMatriz.Add(itemEngComp);
        var questaoAds = TestSeed.Questao("Questão de outra matriz", assuntoRedes.Id);
        questaoAds.AreasCurso.Add(areaEngComp);
        questaoAds.ItensMatriz.Add(itemAds);
        db.Questoes.AddRange(questaoEngComp, questaoAds);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var escopo = new EscopoQuestoes { CursoId = cursoEngComp.Id, MatrizReferenciaId = matrizEngComp.Id };

        var (_, questoes) = await query.ObterCandidatasAsync(escopo, meuId: null, minhaInstituicaoId: null);

        Assert.Single(questoes);
        Assert.Equal(questaoEngComp.Id, questoes[0].Id);
    }

    // Barreira de IDOR usada por ProvaService antes de Criar/Atualizar: um
    // QuestaoId fora do escopo fica fora do conjunto "válidos".
    [Fact]
    public async Task ValidarQuestaoIdsNoEscopoAsync_RejeitaQuestaoForaDoEscopo()
    {
        using var db = TestDbFactory.Criar();

        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo);
        await db.SaveChangesAsync();

        var questaoRedes = TestSeed.Questao("Da disciplina certa", assuntoRedes.Id);
        var questaoSo = TestSeed.Questao("De outra disciplina", assuntoSo.Id);
        db.Questoes.AddRange(questaoRedes, questaoSo);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var escopo = new EscopoQuestoes { DisciplinaIds = new HashSet<int> { redes.Id } };

        var validos = await query.ValidarQuestaoIdsNoEscopoAsync(
            escopo, new List<int> { questaoRedes.Id, questaoSo.Id }, meuId: null, minhaInstituicaoId: null);

        Assert.Contains(questaoRedes.Id, validos);
        Assert.DoesNotContain(questaoSo.Id, validos);
    }
}
