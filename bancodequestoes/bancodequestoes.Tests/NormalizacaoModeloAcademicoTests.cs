using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da normalização acadêmica (AreaCurso/QuestaoAreaCurso/
// CursoDisciplina): compartilhamento entre instituições, visibilidade nunca
// vazando pelo vínculo de área, CursoDisciplina distinta por Curso.
public class NormalizacaoModeloAcademicoTests
{
    // Duas instituições com Cursos apontando pra mesma AreaCurso nacional:
    // uma questão vinculada via QuestaoAreaCurso aparece no pool das duas.
    [Fact]
    public async Task ObterCandidatasAsync_Curso_QuestaoViaAreaCurso_ApareceParaAsDuasInstituicoes()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var cursoInstA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1, areaCursoId: area.Id);
        var cursoInstB = TestSeed.Curso("Engenharia de Computação", instituicaoId: 2, areaCursoId: area.Id);
        db.Cursos.AddRange(cursoInstA, cursoInstB);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assuntoRedes);
        await db.SaveChangesAsync();

        // Nenhum CursoId — só a AreaCurso, exatamente o caso "questão
        // acadêmica sem vínculo institucional nenhum ainda".
        var questao = TestSeed.Questao("Questão nacional de redes", assuntoRedes.Id);
        questao.AreasCurso.Add(area);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);

        var (_, questoesA) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = cursoInstA.Id }, meuId: null, minhaInstituicaoId: null);
        var (_, questoesB) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = cursoInstB.Id }, meuId: null, minhaInstituicaoId: null);

        Assert.Single(questoesA);
        Assert.Single(questoesB);
        Assert.Equal(questao.Id, questoesA[0].Id);
        Assert.Equal(questao.Id, questoesB[0].Id);
    }

    // O fallback legado via Questao.CursoId foi removido; ver o teste
    // equivalente em RemocaoQuestaoCursoLegadoTests.cs.

    // Visibilidade nunca é substituída pelo vínculo de Área: questão Privada
    // de outra instituição não aparece, mesmo com a mesma AreaCurso.
    [Fact]
    public async Task ObterCandidatasAsync_Curso_QuestaoPrivada_NaoVazaPraOutraInstituicaoMesmoComAreaCompartilhada()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var cursoInstA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1, areaCursoId: area.Id);
        var cursoInstB = TestSeed.Curso("Engenharia de Computação", instituicaoId: 2, areaCursoId: area.Id);
        db.Cursos.AddRange(cursoInstA, cursoInstB);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questaoPrivada = TestSeed.Questao("Questão privada do professor de A", assunto.Id);
        questaoPrivada.Visibilidade = VisibilidadeQuestao.Privada;
        questaoPrivada.CriadoPorId = "professor-a";
        questaoPrivada.AreasCurso.Add(area);
        db.Questoes.Add(questaoPrivada);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);

        // "professor-b" pedindo o pool do Curso da Instituição B — a questão
        // é academicamente aplicável (mesma AreaCurso), mas é Privada de
        // outra pessoa, então não pode aparecer.
        var (_, questoesParaB) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = cursoInstB.Id }, meuId: "professor-b", minhaInstituicaoId: 2);

        Assert.Empty(questoesParaB);

        // Controle: o próprio autor, pedindo o pool da Instituição A, precisa
        // continuar vendo a própria questão.
        var (_, questoesParaAutor) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = cursoInstA.Id }, meuId: "professor-a", minhaInstituicaoId: 1);

        Assert.Single(questoesParaAutor);
    }

    // Cobertura curricular (dashboard) precisa contar pelo MESMO critério que
    // o gerador usa pra montar o pool — senão o professor vê "N questões
    // disponíveis" no dashboard e o gerador encontra um número diferente.
    [Fact]
    public async Task ObterCoberturaCurricularAsync_MatrizNacional_ContaQuestaoViaAreaCurso()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matrizNacional = new MatrizReferencia
        {
            Nome = "ENADE 2023",
            AreaCursoId = area.Id,
            Tipo = TipoMatrizReferencia.ENADE,
            Status = StatusMatrizReferencia.Ativa,
        };
        db.MatrizesReferencia.Add(matrizNacional);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questao = TestSeed.Questao("Questão nacional", assunto.Id);
        questao.AreasCurso.Add(area);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var service = TestFakes.NovaMatrizReferenciaService(db);
        var cobertura = await service.ObterCoberturaCurricularAsync(
            matrizNacional.Id, meuId: null, minhaInstituicaoId: null, ehAdmin: true);

        Assert.Equal(1, cobertura.TotalQuestoesDoCurso);
    }

    // CursoDisciplina: duas instituições com Cursos diferentes que ambos têm
    // "Redes de Computadores" na grade compartilham a MESMA Disciplina (é
    // uma entidade global), mas geram duas linhas CursoDisciplina distintas
    // — uma não pode ser confundida com a outra, e o índice único impede
    // duplicar a mesma combinação Curso+Disciplina.
    [Fact]
    public async Task CursoDisciplina_DuasInstituicoesComMesmaDisciplina_GeramVinculosDistintos()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        var cursoB = TestSeed.Curso("Engenharia de Computação", instituicaoId: 2);
        db.Cursos.AddRange(cursoA, cursoB);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes de Computadores");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();

        var service = new CursoService(db);
        var vinculoA = await service.CriarCursoDisciplinaAsync(new CursoDisciplinaInput { CursoId = cursoA.Id, DisciplinaId = redes.Id });
        var vinculoB = await service.CriarCursoDisciplinaAsync(new CursoDisciplinaInput { CursoId = cursoB.Id, DisciplinaId = redes.Id });

        Assert.NotEqual(vinculoA.Id, vinculoB.Id);
        Assert.Equal(redes.Id, vinculoA.DisciplinaId);
        Assert.Equal(redes.Id, vinculoB.DisciplinaId);

        // Duplicar a MESMA combinação Curso+Disciplina precisa falhar —
        // índice único (CursoId, DisciplinaId), checado em
        // ValidarCursoDisciplinaAsync antes mesmo de tentar gravar.
        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarCursoDisciplinaAsync(new CursoDisciplinaInput { CursoId = cursoA.Id, DisciplinaId = redes.Id }));
    }

    // Turma continua criando/atualizando CursoDisciplina automaticamente
    // (upsert lógico, item "não fazer mudança destrutiva em Turma") — uma
    // Turma nova de uma combinação Curso+Disciplina ainda não registrada
    // gera a linha sozinha, sem exigir cadastro manual prévio.
    [Fact]
    public async Task CriarTurmaAsync_SemCursoDisciplinaPrevia_CriaAutomaticamente()
    {
        using var db = TestDbFactory.Criar();

        // Instituicao "de verdade" (não só um InstituicaoId solto) — em
        // produção o FK garante que ela sempre existe; ValidarTurmaAsync
        // consulta Curso.Instituicao.SistemaPeriodos, então precisa de uma
        // linha real aqui pro InMemory provider resolver o join.
        var instituicao = new Instituicao { Nome = "Instituição A", SistemaPeriodos = SistemaPeriodos.Semestral };
        db.Instituicoes.Add(instituicao);
        await db.SaveChangesAsync();

        var curso = TestSeed.Curso("Engenharia de Computação", instituicaoId: instituicao.Id);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes de Computadores");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();

        var service = new CursoService(db);
        await service.CriarTurmaAsync(
            new TurmaInput { Nome = "Turma A", CursoId = curso.Id, DisciplinaId = redes.Id, Ano = 2026, Semestre = 1 },
            criadoPorId: null);

        var vinculos = await service.ListarDisciplinasDoCursoAsync(curso.Id);

        Assert.Single(vinculos);
        Assert.Equal(redes.Id, vinculos[0].DisciplinaId);
    }
}
