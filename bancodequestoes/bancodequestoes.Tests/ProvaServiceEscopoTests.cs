using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes de ProvaService com Escopo (Disciplina/Multidisciplinar/Curso)
// contra InMemory: validação, IDOR, compatibilidade com formato antigo, GerarAsync.
public class ProvaServiceEscopoTests
{
    private static ProvaService NovoServico(BancoQuestoes.Data.ApplicationDbContext db) =>
        new(db, new QuestaoQueryService(db), new GeradorProvaService(), TestFakes.NovaMatrizReferenciaService(db));

    [Fact]
    public async Task CriarAsync_Multidisciplinar_DisciplinaSemTurmaNoCurso_LancaExcecao()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();

        // Só Redes tem Turma cadastrada neste curso — SO não pertence a ele
        // (nenhuma relação real, ver comentário em models/Disciplina.cs).
        db.Turmas.Add(TestSeed.Turma("EC1A", curso.Id, redes.Id));
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assuntoRedes);
        await db.SaveChangesAsync();
        var questao = TestSeed.Questao("Q1", assuntoRedes.Id);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var modelo = new ProvaInput
        {
            Titulo = "Prova multidisciplinar inválida",
            TipoEscopo = TipoEscopoProva.Multidisciplinar,
            CursoId = curso.Id,
            DisciplinaIds = new HashSet<int> { redes.Id, so.Id },
        };
        var selecionadas = new List<QuestaoSelecionada> { new() { QuestaoId = questao.Id, Ordem = 0 } };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(modelo, selecionadas, criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    [Fact]
    public async Task CriarAsync_Multidisciplinar_ComTurmasValidas_CriaProvaDisciplinas()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();

        db.Turmas.AddRange(
            TestSeed.Turma("EC1A", curso.Id, redes.Id),
            TestSeed.Turma("EC1B", curso.Id, so.Id));
        await db.SaveChangesAsync();

        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo);
        await db.SaveChangesAsync();

        var q1 = TestSeed.Questao("Q Redes", assuntoRedes.Id);
        var q2 = TestSeed.Questao("Q SO", assuntoSo.Id);
        db.Questoes.AddRange(q1, q2);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var modelo = new ProvaInput
        {
            Titulo = "Prova multidisciplinar",
            TipoEscopo = TipoEscopoProva.Multidisciplinar,
            CursoId = curso.Id,
            DisciplinaIds = new HashSet<int> { redes.Id, so.Id },
            DistribuicaoDisciplinas = new Dictionary<int, int> { [redes.Id] = 60, [so.Id] = 40 },
        };
        var selecionadas = new List<QuestaoSelecionada>
        {
            new() { QuestaoId = q1.Id, Ordem = 0 },
            new() { QuestaoId = q2.Id, Ordem = 1 },
        };

        var prova = await servico.CriarAsync(modelo, selecionadas, criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(TipoEscopoProva.Multidisciplinar, prova.TipoEscopo);
        Assert.Null(prova.DisciplinaId);
        Assert.Equal(2, prova.ProvaDisciplinas.Count);
        Assert.Equal(2, prova.ProvaQuestoes.Count);
        Assert.Equal(60, prova.ProvaDisciplinas.Single(pd => pd.DisciplinaId == redes.Id).PercentualPlanejado);
    }

    [Fact]
    public async Task CriarAsync_Curso_MatrizDeOutroCurso_LancaExcecao()
    {
        using var db = TestDbFactory.Criar();

        var cursoEngComp = TestSeed.Curso("Engenharia de Computação");
        var cursoAds = TestSeed.Curso("ADS");
        db.Cursos.AddRange(cursoEngComp, cursoAds);
        await db.SaveChangesAsync();

        var matrizDeAds = TestSeed.Matriz("ENADE — ADS", cursoAds.Id);
        db.MatrizesReferencia.Add(matrizDeAds);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        var questao = TestSeed.Questao("Q1", assunto.Id);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var modelo = new ProvaInput
        {
            Titulo = "Simulado com matriz errada",
            TipoEscopo = TipoEscopoProva.Curso,
            CursoId = cursoEngComp.Id,
            MatrizReferenciaId = matrizDeAds.Id, // pertence a outro curso
        };
        var selecionadas = new List<QuestaoSelecionada> { new() { QuestaoId = questao.Id, Ordem = 0 } };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(modelo, selecionadas, criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    // Mesmo com um QuestaoId forjado fora do escopo, CriarAsync rejeita a
    // operação inteira, nunca salva parcialmente.
    [Fact]
    public async Task CriarAsync_QuestaoForaDoEscopoDeclarado_LancaExcecao()
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

        var qRedes = TestSeed.Questao("Da disciplina certa", assuntoRedes.Id);
        var qSo = TestSeed.Questao("De outra disciplina", assuntoSo.Id);
        db.Questoes.AddRange(qRedes, qSo);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var modelo = new ProvaInput
        {
            Titulo = "Prova de Redes",
            TipoEscopo = TipoEscopoProva.Disciplina,
            DisciplinaId = redes.Id,
        };
        var selecionadas = new List<QuestaoSelecionada>
        {
            new() { QuestaoId = qRedes.Id, Ordem = 0 },
            new() { QuestaoId = qSo.Id, Ordem = 1 }, // injetada, fora do escopo
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(modelo, selecionadas, criadoPorId: "prof-1", minhaInstituicaoId: null));
    }

    // Simula o backfill da migration (Prova só com DisciplinaId); não
    // substitui rodar a migration de verdade.
    [Fact]
    public async Task CarregarComQuestoesAsync_ProvaNoFormatoAntigo_LeCorretamenteComoEscopoDisciplina()
    {
        using var db = TestDbFactory.Criar();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        var questao = TestSeed.Questao("Q1", assunto.Id);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var provaAntiga = new Prova
        {
            Titulo = "Prova criada antes da evolução de Escopo",
            DisciplinaId = redes.Id,
            CriadoPorId = "prof-1",
            // TipoEscopo deliberadamente OMITIDO — usa o default da classe
            // (Disciplina), igual o default da coluna no banco pós-migração.
        };
        provaAntiga.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0 });
        db.Provas.Add(provaAntiga);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var carregada = await servico.CarregarComQuestoesAsync(provaAntiga.Id);

        Assert.NotNull(carregada);
        Assert.Equal(TipoEscopoProva.Disciplina, carregada!.TipoEscopo);
        Assert.Equal(redes.Id, carregada.DisciplinaId);
        Assert.Empty(carregada.ProvaDisciplinas);
        Assert.Single(carregada.ProvaQuestoes);
    }

    // Salvar de novo sem mudar nada relevante mantém questões e escopo intactos.
    [Fact]
    public async Task AtualizarAsync_EditarSemMudarEscopo_NaoPerdeQuestoesNemDistribuicao()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();
        db.Turmas.AddRange(
            TestSeed.Turma("EC1A", curso.Id, redes.Id),
            TestSeed.Turma("EC1B", curso.Id, so.Id));
        await db.SaveChangesAsync();
        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo);
        await db.SaveChangesAsync();
        var q1 = TestSeed.Questao("Q Redes", assuntoRedes.Id);
        var q2 = TestSeed.Questao("Q SO", assuntoSo.Id);
        db.Questoes.AddRange(q1, q2);
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var modelo = new ProvaInput
        {
            Titulo = "Prova multidisciplinar",
            TipoEscopo = TipoEscopoProva.Multidisciplinar,
            CursoId = curso.Id,
            DisciplinaIds = new HashSet<int> { redes.Id, so.Id },
            DistribuicaoDisciplinas = new Dictionary<int, int> { [redes.Id] = 50, [so.Id] = 50 },
        };
        var selecionadas = new List<QuestaoSelecionada>
        {
            new() { QuestaoId = q1.Id, Ordem = 0 },
            new() { QuestaoId = q2.Id, Ordem = 1 },
        };
        var criada = await servico.CriarAsync(modelo, selecionadas, criadoPorId: "prof-1", minhaInstituicaoId: null);

        await servico.AtualizarAsync(criada.Id, modelo, selecionadas, meuId: "prof-1", minhaInstituicaoId: null);

        var releida = await servico.CarregarComQuestoesAsync(criada.Id);
        Assert.NotNull(releida);
        Assert.Equal(2, releida!.ProvaQuestoes.Count);
        Assert.Equal(2, releida.ProvaDisciplinas.Count);
    }

    // GerarAsync no modo Multidisciplinar: distribuição real bate com o
    // planejado quando há disponibilidade suficiente.
    [Fact]
    public async Task GerarAsync_Multidisciplinar_DistribuicaoPorDisciplina_BateComOPlanejado()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var redes = TestSeed.Disciplina("Redes");
        var so = TestSeed.Disciplina("Sistemas Operacionais");
        db.Disciplinas.AddRange(redes, so);
        await db.SaveChangesAsync();
        var assuntoRedes = TestSeed.Assunto("Roteamento", redes.Id);
        var assuntoSo = TestSeed.Assunto("Escalonamento", so.Id);
        db.Assuntos.AddRange(assuntoRedes, assuntoSo);
        await db.SaveChangesAsync();

        for (var i = 0; i < 6; i++)
        {
            db.Questoes.Add(TestSeed.Questao($"Q Redes {i}", assuntoRedes.Id));
        }
        for (var i = 0; i < 4; i++)
        {
            db.Questoes.Add(TestSeed.Questao($"Q SO {i}", assuntoSo.Id));
        }
        await db.SaveChangesAsync();

        var servico = NovoServico(db);
        var gerador = new GeradorInput
        {
            Quantidade = 10,
            PercFacil = 0,
            PercMedia = 100,
            PercDificil = 0,
            Escopo = new EscopoProvaConfig
            {
                Tipo = TipoEscopoProva.Multidisciplinar,
                CursoId = curso.Id,
                DisciplinaIds = new HashSet<int> { redes.Id, so.Id },
            },
            DistribuicaoDisciplinas = new Dictionary<int, int> { [redes.Id] = 60, [so.Id] = 40 },
        };

        var resultado = await servico.GerarAsync(gerador, meuId: null, minhaInstituicaoId: null, idsJaSelecionados: new HashSet<int>());

        Assert.False(resultado.Abortado);
        Assert.Equal(10, resultado.Questoes.Count);
        Assert.Equal(6, resultado.DistribuicaoPorDisciplina.GetValueOrDefault(redes.Id));
        Assert.Equal(4, resultado.DistribuicaoPorDisciplina.GetValueOrDefault(so.Id));
        Assert.Equal(resultado.Questoes.Count, resultado.Questoes.Select(q => q.Id).Distinct().Count());
    }
}
