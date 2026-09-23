using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "eliminar definitivamente o legado Questao.CursoId" —
// escritos ANTES da remoção do fallback/propriedade (Etapa 4 do pedido),
// descrevendo o comportamento final desejado: QuestaoAreaCurso é a ÚNICA
// fonte de verdade pra "esta questão serve pra este Curso/Área", sem
// fallback nenhum pro campo legado. Alguns destes testes FALHAM enquanto o
// fallback (QuestaoQueryService) e as três atribuições (QuestaoService)
// ainda existem — isso é esperado (TDD): eles passam a passar sozinhos
// assim que as Etapas 5-7 remoerem o fallback e as dependências, sem
// precisar editar o teste de novo.
public class RemocaoQuestaoCursoLegadoTests
{
    // Substitui o teste obsoleto "...QuestaoLegadaSoComCursoId_ContinuaAparecendo"
    // (removido — testava exatamente o fallback que está sendo eliminado).
    // Cenário oposto: um Curso com AreaCursoId definida, mas a questão NÃO
    // tem nenhum vínculo em QuestaoAreaCurso — sem fallback, ela não pode
    // aparecer no pool. "Detectar e não inventar" (item central do pedido):
    // uma questão sem vínculo de área simplesmente não é candidata, nunca é
    // assumida como pertencente por estar "perto" de um Curso.
    [Fact]
    public async Task ObterCandidatasAsync_Curso_QuestaoSemQuestaoAreaCurso_NaoAparece()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var curso = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1, areaCursoId: area.Id);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        // Nenhum AreasCurso.Add — questão "solta", sem aplicabilidade
        // acadêmica declarada nenhuma.
        var questaoSemVinculo = TestSeed.Questao("Questão sem vínculo de área", assunto.Id);
        db.Questoes.Add(questaoSemVinculo);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var (_, questoes) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = curso.Id }, meuId: null, minhaInstituicaoId: null);

        Assert.Empty(questoes);
    }

    // Curso sem AreaCursoId nenhuma configurada: sem fallback, não existe
    // mais nenhum jeito de traduzir "este Curso" em critério de busca — o
    // pool tem que vir vazio (não é erro, é "configure a Área de Curso
    // primeiro"), mesmo que exista uma questão vinculada a outra área
    // qualquer no banco.
    [Fact]
    public async Task ObterCandidatasAsync_Curso_SemAreaCursoNoCurso_PoolVazio()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Curso sem Área definida", instituicaoId: 1, areaCursoId: null);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var outraArea = TestSeed.AreaCurso("Outra área qualquer");
        db.AreasCurso.Add(outraArea);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questao = TestSeed.Questao("Questão de outra área", assunto.Id);
        questao.AreasCurso.Add(outraArea);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var (_, questoes) = await query.ObterCandidatasAsync(
            new EscopoQuestoes { CursoId = curso.Id }, meuId: null, minhaInstituicaoId: null);

        Assert.Empty(questoes);
    }

    // Mesma regra acima, mas pelo caminho de validação de IDOR (item 13/94-96
    // do pedido) — um QuestaoId forjado no POST não pode ser aceito só
    // porque em algum momento existiu Questao.CursoId; sem AreaCursoId no
    // Curso do escopo, NADA valida.
    [Fact]
    public async Task ValidarQuestaoIdsNoEscopoAsync_Curso_SemAreaCursoNoCurso_RejeitaTudo()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Curso sem Área definida", instituicaoId: 1, areaCursoId: null);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questao = TestSeed.Questao("Questão qualquer", assunto.Id);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var validos = await query.ValidarQuestaoIdsNoEscopoAsync(
            new EscopoQuestoes { CursoId = curso.Id }, new List<int> { questao.Id }, meuId: null, minhaInstituicaoId: null);

        Assert.Empty(validos);
    }

    // Tela de listagem/filtro: QuestaoFiltro.CursoId vira PURAMENTE escopo de
    // UI (cascata do <select> de Matriz/Item) — nunca mais filtra Questao
    // diretamente. Marcar um CursoId no filtro, sozinho, não pode reduzir o
    // resultado da listagem (só AreaCursoId/MatrizReferenciaId/ItemMatrizId
    // filtram de fato).
    [Fact]
    public async Task ListarAsync_FiltroCursoId_NaoFiltraQuestaoDiretamente()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        db.Questoes.AddRange(
            TestSeed.Questao("Q1", assunto.Id),
            TestSeed.Questao("Q2", assunto.Id));
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);

        var semFiltroCurso = await query.ListarAsync(new QuestaoFiltro(), meuId: null, minhaInstituicaoId: null, pagina: 1, tamanhoPagina: 20);
        var comFiltroCurso = await query.ListarAsync(new QuestaoFiltro { CursoId = curso.Id }, meuId: null, minhaInstituicaoId: null, pagina: 1, tamanhoPagina: 20);

        Assert.Equal(semFiltroCurso.Total, comFiltroCurso.Total);
        Assert.Equal(2, comFiltroCurso.Total);
    }

    // Contraste com o teste acima: AreaCursoId CONTINUA filtrando de fato —
    // é o filtro correto pra "questões desta área", diferente de CursoId.
    [Fact]
    public async Task ListarAsync_FiltroAreaCursoId_FiltraDeFato()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        var outraArea = TestSeed.AreaCurso("Outra área");
        db.AreasCurso.AddRange(area, outraArea);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var daArea = TestSeed.Questao("Da área certa", assunto.Id);
        daArea.AreasCurso.Add(area);
        var deOutraArea = TestSeed.Questao("De outra área", assunto.Id);
        deOutraArea.AreasCurso.Add(outraArea);
        db.Questoes.AddRange(daArea, deOutraArea);
        await db.SaveChangesAsync();

        var query = new QuestaoQueryService(db);
        var resultado = await query.ListarAsync(new QuestaoFiltro { AreaCursoId = area.Id }, meuId: null, minhaInstituicaoId: null, pagina: 1, tamanhoPagina: 20);

        Assert.Equal(1, resultado.Total);
        Assert.Equal(daArea.Id, resultado.Itens[0].Id);
    }

    // Dashboard de Cobertura Curricular pra matriz INSTITUCIONAL (PPC,
    // CursoId-escopada): sem Questao.CursoId, o denominador "total de
    // questões do curso" muda de "toda questão com esse CursoId" pra "toda
    // questão com pelo menos um Item vinculado a ESTA matriz" — mudança de
    // comportamento documentada (ver relatório final), não um bug. Uma
    // questão com Item de OUTRA matriz do mesmo curso não conta aqui.
    [Fact]
    public async Task ObterCoberturaCurricularAsync_MatrizInstitucional_ContaPorItemVinculado()
    {
        using var db = TestDbFactory.Criar();

        var curso = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var matriz = TestSeed.Matriz("PPC 2024", curso.Id, TipoMatrizReferencia.PPC);
        var outraMatriz = TestSeed.Matriz("PPC antigo", curso.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.AddRange(matriz, outraMatriz);
        await db.SaveChangesAsync();

        var item = TestSeed.Item("C01", "Competência 1", matriz.Id);
        var itemOutraMatriz = TestSeed.Item("X01", "Item de outra matriz", outraMatriz.Id);
        db.ItensMatrizReferencia.AddRange(item, itemOutraMatriz);
        await db.SaveChangesAsync();

        var redes = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(redes);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", redes.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var questaoComItem = TestSeed.Questao("Vinculada à matriz certa", assunto.Id);
        questaoComItem.ItensMatriz.Add(item);
        var questaoDeOutraMatriz = TestSeed.Questao("Vinculada a outra matriz do mesmo curso", assunto.Id);
        questaoDeOutraMatriz.ItensMatriz.Add(itemOutraMatriz);
        var questaoSemItem = TestSeed.Questao("Sem nenhum item vinculado", assunto.Id);
        db.Questoes.AddRange(questaoComItem, questaoDeOutraMatriz, questaoSemItem);
        await db.SaveChangesAsync();

        var service = TestFakes.NovaMatrizReferenciaService(db);
        var cobertura = await service.ObterCoberturaCurricularAsync(matriz.Id, meuId: null, minhaInstituicaoId: 1, ehAdmin: true);

        Assert.Equal(1, cobertura.TotalQuestoesDoCurso);
    }

    // Criar uma questão sem tocar em nenhum conceito de Curso — só
    // AreaCursoIds — precisa funcionar de ponta a ponta (QuestaoService não
    // depende mais de Curso pra academicamente vincular uma questão).
    [Fact]
    public async Task CriarAsync_SoComAreaCursoIds_PersisteVinculoDeArea()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Ciência da Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var disc = TestSeed.Disciplina("Algoritmos");
        db.Disciplinas.Add(disc);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Ordenação", disc.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        var service = TestFakes.NovoQuestaoService(db);
        var modelo = new QuestaoInput
        {
            AssuntoId = assunto.Id,
            Enunciado = "Qual a complexidade do quicksort no pior caso?",
            AreaCursoIds = new List<int> { area.Id },
            Alternativas = new List<AlternativaInput> { new() { Texto = "O(n)" }, new() { Texto = "O(n^2)" } },
            RespostaCorretaIndex = 1,
        };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.AreasCurso).LoadAsync();
        Assert.Single(recarregada!.AreasCurso);
        Assert.Equal(area.Id, recarregada.AreasCurso[0].Id);
    }
}
