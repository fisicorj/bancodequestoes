using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa AreaCursoService.MesclarAsync: só Admin mescla, reatribuição correta,
// sem colisão de PK em vínculo duplicado, diagnóstico de Matriz ENADE dupla.
public class AreaCursoServiceMesclagemTests
{
    [Fact]
    public async Task MesclarAsync_SemEhAdmin_Rejeita()
    {
        using var db = TestDbFactory.Criar();

        var origem = TestSeed.AreaCurso("Eng. da Computação");
        var destino = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(origem, destino);
        await db.SaveChangesAsync();

        var service = new AreaCursoService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.MesclarAsync(origem.Id, destino.Id, ehAdmin: false));
    }

    [Fact]
    public async Task MesclarAsync_ReatribuiCursosMatrizesEQuestoes_EExcluiOrigem()
    {
        using var db = TestDbFactory.Criar();

        var origem = TestSeed.AreaCurso("Eng. da Computação");
        var destino = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(origem, destino);
        await db.SaveChangesAsync();

        var curso = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1, areaCursoId: origem.Id);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var matriz = new MatrizReferencia
        {
            Nome = "ENADE 2023",
            AreaCursoId = origem.Id,
            Tipo = TipoMatrizReferencia.ENADE,
            Status = StatusMatrizReferencia.Ativa,
        };
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questao = TestSeed.Questao("Questão de redes", assunto.Id);
        questao.AreasCurso.Add(origem);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var service = new AreaCursoService(db);
        var resultado = await service.MesclarAsync(origem.Id, destino.Id, ehAdmin: true);

        Assert.Equal(1, resultado.CursosMovidos);
        Assert.Equal(1, resultado.MatrizesMovidas);
        Assert.Equal(1, resultado.QuestoesMovidas);
        Assert.False(resultado.RevisarMatrizesEnadeAtivasNoDestino);

        Assert.Null(await db.AreasCurso.FindAsync(origem.Id));

        var cursoRecarregado = await db.Cursos.FindAsync(curso.Id);
        Assert.Equal(destino.Id, cursoRecarregado!.AreaCursoId);

        var matrizRecarregada = await db.MatrizesReferencia.FindAsync(matriz.Id);
        Assert.Equal(destino.Id, matrizRecarregada!.AreaCursoId);

        var vinculoQuestao = await db.QuestoesAreasCurso.FindAsync(questao.Id, destino.Id);
        Assert.NotNull(vinculoQuestao);
    }

    // Vínculo duplo antes da mesclagem não pode gerar erro de PK; só sobra o vínculo com o destino.
    [Fact]
    public async Task MesclarAsync_QuestaoJaVinculadaAsDuasAreas_NaoDuplicaVinculo()
    {
        using var db = TestDbFactory.Criar();

        var origem = TestSeed.AreaCurso("Eng. da Computação");
        var destino = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(origem, destino);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questao = TestSeed.Questao("Questão em ambas", assunto.Id);
        questao.AreasCurso.Add(origem);
        questao.AreasCurso.Add(destino);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var service = new AreaCursoService(db);
        var resultado = await service.MesclarAsync(origem.Id, destino.Id, ehAdmin: true);

        // Colisão: o vínculo da origem foi descartado (o do destino já
        // existia), não contado como "movido".
        Assert.Equal(0, resultado.QuestoesMovidas);

        var vinculosRestantes = db.QuestoesAreasCurso.Where(qa => qa.QuestaoId == questao.Id).ToList();
        Assert.Single(vinculosRestantes);
        Assert.Equal(destino.Id, vinculosRestantes[0].AreaCursoId);
    }

    // Duas Matrizes ENADE Ativas (uma por área) ficam as DUAS Ativas no
    // destino — o Service nunca decide sozinho qual desativar.
    [Fact]
    public async Task MesclarAsync_DuasMatrizesEnadeAtivas_ReportaRevisaoSemDecidirSozinho()
    {
        using var db = TestDbFactory.Criar();

        var origem = TestSeed.AreaCurso("Eng. da Computação");
        var destino = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.AddRange(origem, destino);
        await db.SaveChangesAsync();

        db.MatrizesReferencia.AddRange(
            new MatrizReferencia { Nome = "ENADE 2023 (origem)", AreaCursoId = origem.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa },
            new MatrizReferencia { Nome = "ENADE 2023 (destino)", AreaCursoId = destino.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa });
        await db.SaveChangesAsync();

        var service = new AreaCursoService(db);
        var resultado = await service.MesclarAsync(origem.Id, destino.Id, ehAdmin: true);

        Assert.True(resultado.RevisarMatrizesEnadeAtivasNoDestino);

        var ativasNoDestino = await db.MatrizesReferencia
            .Where(m => m.AreaCursoId == destino.Id && m.Status == StatusMatrizReferencia.Ativa)
            .ToListAsync();
        Assert.Equal(2, ativasNoDestino.Count);
    }
}
