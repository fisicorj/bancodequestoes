using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa ImportacaoService: delegação pro parser certo (Analisar), anotação
// de códigos de Item de Matriz não encontrados, resolução de grupos
// "$ASSUNTO:" (existente/homônimo/novo) e a importação de fato (ImportarAsync).
public class ImportacaoServiceTests
{
    private static ImportacaoService NovoService(BancoQuestoes.Data.ApplicationDbContext db) =>
        new(db, TestFakes.NovaMatrizReferenciaService(db), new DisciplinaService(db));

    [Fact]
    public void Analisar_FormatoGift_UsaGiftParser()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);

        var resultado = service.Analisar("Qual a capital da França? { =Paris ~Londres ~Berlim }", "Gift");

        var questao = Assert.Single(resultado.Questoes);
        Assert.Equal(TipoQuestao.MultiplaEscolha, questao.Tipo);
    }

    [Fact]
    public void Analisar_FormatoAiken_UsaAikenParser()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);

        var resultado = service.Analisar("Qual a capital da França?\nA) Paris\nB) Londres\nANSWER: A\n", "Aiken");

        var questao = Assert.Single(resultado.Questoes);
        Assert.Equal(TipoQuestao.MultiplaEscolha, questao.Tipo);
        Assert.True(questao.Alternativas[0].Correta);
    }

    // --- AnotarItensMatrizAsync ---

    [Fact]
    public async Task AnotarItensMatrizAsync_SemCurso_NaoAlteraNada()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, CodigosItemMatriz = { "C1" } } },
        };

        await service.AnotarItensMatrizAsync(resultado, cursoId: null);

        Assert.Empty(resultado.Questoes[0].CodigosNaoEncontrados);
    }

    [Fact]
    public async Task AnotarItensMatrizAsync_CodigoExistenteNoCurso_NaoMarcaComoNaoEncontrado()
    {
        using var db = TestDbFactory.Criar();
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var matriz = TestSeed.Matriz("ENADE 2024", curso.Id);
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();
        db.ItensMatrizReferencia.Add(TestSeed.Item("C1", "Competência 1", matriz.Id));
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, CodigosItemMatriz = { "C1" } } },
        };

        await service.AnotarItensMatrizAsync(resultado, curso.Id);

        Assert.Empty(resultado.Questoes[0].CodigosNaoEncontrados);
    }

    [Fact]
    public async Task AnotarItensMatrizAsync_CodigoInexistente_Marca()
    {
        using var db = TestDbFactory.Criar();
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var matriz = TestSeed.Matriz("ENADE 2024", curso.Id);
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();
        db.ItensMatrizReferencia.Add(TestSeed.Item("C1", "Competência 1", matriz.Id));
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, CodigosItemMatriz = { "C99" } } },
        };

        await service.AnotarItensMatrizAsync(resultado, curso.Id);

        Assert.Equal(new[] { "C99" }, resultado.Questoes[0].CodigosNaoEncontrados);
    }

    // --- ResolverAssuntosAsync ---

    [Fact]
    public async Task ResolverAssuntosAsync_CombinacaoJaExiste_UsaExistente()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Matemática");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Álgebra", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, DisciplinaSugerida = "matemática", AssuntoSugerido = "álgebra" } },
        };

        await service.ResolverAssuntosAsync(resultado);

        var grupo = Assert.Single(resultado.GruposAssunto);
        Assert.False(grupo.CriarNovo);
        Assert.Equal(assunto.Id, grupo.AssuntoIdEncontrado);
        Assert.Equal(assunto.Id, grupo.AssuntoIdEscolhido);
        Assert.Equal(disciplina.Id, grupo.DisciplinaIdEncontrada);
    }

    [Fact]
    public async Task ResolverAssuntosAsync_NadaParecido_ProdeCriarNovo()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, DisciplinaSugerida = "Biologia", AssuntoSugerido = "Genética" } },
        };

        await service.ResolverAssuntosAsync(resultado);

        var grupo = Assert.Single(resultado.GruposAssunto);
        Assert.True(grupo.CriarNovo);
        Assert.Null(grupo.DisciplinaHomonimaEncontradaId);
    }

    [Fact]
    public async Task ResolverAssuntosAsync_AssuntoBateComDisciplinaExistente_NaoPropoeCriar()
    {
        using var db = TestDbFactory.Criar();
        var fisica = TestSeed.Disciplina("Física");
        db.Disciplinas.Add(fisica);
        await db.SaveChangesAsync();
        db.Assuntos.Add(TestSeed.Assunto("Mecânica", fisica.Id));
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            // "Física" (o ASSUNTO sugerido) é homônimo de uma Disciplina que já existe.
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, DisciplinaSugerida = "Outra Disciplina", AssuntoSugerido = "Física" } },
        };

        await service.ResolverAssuntosAsync(resultado);

        var grupo = Assert.Single(resultado.GruposAssunto);
        Assert.False(grupo.CriarNovo);
        Assert.Equal(fisica.Id, grupo.DisciplinaHomonimaEncontradaId);
    }

    // --- ImportarAsync ---

    [Fact]
    public async Task ImportarAsync_SemAssuntoDaTelaQuandoNecessario_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes = { new QuestaoImportada { Enunciado = "Q1", Tipo = TipoQuestao.Discursiva, Selecionada = true } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => service.ImportarAsync(resultado, assuntoId: 0, Dificuldade.Media, cursoId: null, criadoPorId: "prof-1"));
    }

    [Fact]
    public async Task ImportarAsync_GrupoCriarNovoSemNomes_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            GruposAssunto = { new GrupoAssuntoImportacao { Disciplina = "D", Assunto = "A", CriarNovo = true, NomeDisciplinaFinal = "", NomeAssuntoFinal = "" } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => service.ImportarAsync(resultado, assuntoId: 1, Dificuldade.Media, cursoId: null, criadoPorId: "prof-1"));
    }

    [Fact]
    public async Task ImportarAsync_GrupoExistenteSemEscolha_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            GruposAssunto = { new GrupoAssuntoImportacao { Disciplina = "D", Assunto = "A", CriarNovo = false, AssuntoIdEscolhido = 0 } },
        };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => service.ImportarAsync(resultado, assuntoId: 1, Dificuldade.Media, cursoId: null, criadoPorId: "prof-1"));
    }

    [Fact]
    public async Task ImportarAsync_Simples_CriaQuestaoComAssuntoDaTela()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Matemática");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Álgebra", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes =
            {
                new QuestaoImportada
                {
                    Enunciado = "Quanto é 2+2?",
                    Tipo = TipoQuestao.MultiplaEscolha,
                    Alternativas = { new AlternativaImportada { Texto = "4", Correta = true }, new AlternativaImportada { Texto = "5", Correta = false } },
                    Selecionada = true,
                },
            },
        };

        var quantidade = await service.ImportarAsync(resultado, assunto.Id, Dificuldade.Facil, cursoId: null, criadoPorId: "prof-1");

        Assert.Equal(1, quantidade);
        var questao = Assert.Single(db.Questoes);
        Assert.Equal(assunto.Id, questao.AssuntoId);
        Assert.Equal(Dificuldade.Facil, questao.Dificuldade);
        Assert.Equal(TipoQuestao.MultiplaEscolha, questao.TipoQuestao);
        Assert.Equal("prof-1", questao.CriadoPorId);
        Assert.Equal(OrigemQuestao.Importada, questao.Origem);
    }

    [Fact]
    public async Task ImportarAsync_QuestaoNaoSelecionada_NaoImporta()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Matemática");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Álgebra", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes =
            {
                new QuestaoImportada
                {
                    Enunciado = "Não deve importar",
                    Tipo = TipoQuestao.Discursiva,
                    Selecionada = false,
                },
            },
        };

        var quantidade = await service.ImportarAsync(resultado, assunto.Id, Dificuldade.Facil, cursoId: null, criadoPorId: "prof-1");

        Assert.Equal(0, quantidade);
        Assert.Empty(db.Questoes);
    }

    [Fact]
    public async Task ImportarAsync_ComAssuntoNovoDoGrupo_CriaDisciplinaEAssunto()
    {
        using var db = TestDbFactory.Criar();
        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes =
            {
                new QuestaoImportada
                {
                    Enunciado = "Q1",
                    Tipo = TipoQuestao.Discursiva,
                    DisciplinaSugerida = "Biologia",
                    AssuntoSugerido = "Genética",
                    Selecionada = true,
                },
            },
        };

        await service.ResolverAssuntosAsync(resultado);
        var quantidade = await service.ImportarAsync(resultado, assuntoId: 0, Dificuldade.Media, cursoId: null, criadoPorId: "prof-1");

        Assert.Equal(1, quantidade);
        var disciplinaCriada = Assert.Single(db.Disciplinas);
        Assert.Equal("Biologia", disciplinaCriada.Nome);
        var assuntoCriado = Assert.Single(db.Assuntos);
        Assert.Equal("Genética", assuntoCriado.Nome);
        var questao = Assert.Single(db.Questoes);
        Assert.Equal(assuntoCriado.Id, questao.AssuntoId);
    }

    [Fact]
    public async Task ImportarAsync_ComCodigoItemMatriz_VinculaItem()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Matemática");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Álgebra", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var matriz = TestSeed.Matriz("ENADE 2024", curso.Id);
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C1", "Competência 1", matriz.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var service = NovoService(db);
        var resultado = new ResultadoImportacao
        {
            Questoes =
            {
                new QuestaoImportada
                {
                    Enunciado = "Q1",
                    Tipo = TipoQuestao.Discursiva,
                    CodigosItemMatriz = { "C1" },
                    Selecionada = true,
                },
            },
        };

        await service.ImportarAsync(resultado, assunto.Id, Dificuldade.Media, curso.Id, "prof-1");

        var questaoComItens = await db.Questoes.Include(q => q.ItensMatriz).SingleAsync();
        Assert.Contains(questaoComItens.ItensMatriz, i => i.Codigo == "C1");
    }
}
