using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "Ajustes finais de integridade curricular — Banco de
// Questões" (2ª rodada de revisão sobre a Normalização do Modelo Acadêmico).
// Cobre os três problemas de backend identificados na revisão:
//
//   1) item de matriz INATIVO sendo aceito numa gravação nova
//      (QuestaoCurricularService.ResolverItensMatrizAsync + MatrizReferenciaService.
//      ValidarItensDoCursoAsync/ValidarItensPorAreasCursoAsync não
//      garantiam i.Ativo em todos os caminhos);
//   2) item pertencente a matriz em Status=Rascunho sendo aceito do mesmo
//      jeito (faltava Status != Rascunho nos mesmos métodos);
//   3) ItemMatrizId inexistente sendo descartado em silêncio em vez de
//      rejeitar a operação inteira.
//
// Mais os testes de HISTÓRICO (itens 19-20/38-39 do pedido): um vínculo que
// a questão já tinha ANTES da edição não pode ser bloqueado nem apagado só
// porque o item foi desativado (ou a matriz virou Rascunho) DEPOIS — só uma
// tentativa de criar uma associação NOVA com um item nessas condições é que
// deve ser rejeitada.
//
// Os testes 1-4 do pedido (bloqueio de remoção de Área/troca de Curso NA UI)
// não são cobertos aqui: este projeto de testes é só de Services (ver
// comentário em bancodequestoes.Tests.csproj) — não há bUnit nem qualquer
// infraestrutura de teste de componente Blazor no repositório. Esse
// comportamento (QuestaoForm.razor/QuestaoAlinhamentoCurricularSection.razor)
// foi verificado por leitura de código; fica documentado como pendência real
// no relatório final.
public class AjustesFinaisIntegridadeCurricularTests
{
    private static QuestaoService NovoService(BancoQuestoes.Data.ApplicationDbContext db) =>
        TestFakes.NovoQuestaoService(db);

    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedDisciplinaAssuntoAsync(BancoQuestoes.Data.ApplicationDbContext db)
    {
        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return (disciplina, assunto);
    }

    private static QuestaoInput ModeloBase(int assuntoId) => new()
    {
        AssuntoId = assuntoId,
        Enunciado = "Questão de teste",
        Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
    };

    // Teste 5 do pedido — item inativo: CriarAsync com um ItemMatrizId cujo
    // Ativo=false precisa ser rejeitado, não aceito.
    [Fact]
    public async Task CriarAsync_ItemMatrizInativo_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matriz = new MatrizReferencia { Nome = "ENADE 2023", AreaCursoId = area.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Ativa };
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();
        var itemInativo = new ItemMatrizReferencia { Codigo = "C08", Titulo = "Competência 8", MatrizReferenciaId = matriz.Id, Ativo = false };
        db.ItensMatrizReferencia.Add(itemInativo);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloBase(assunto.Id);
        modelo.AreaCursoIds = new List<int> { area.Id };
        modelo.ItemMatrizIds = new List<int> { itemInativo.Id };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("inativ", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Teste 6 do pedido — matriz Rascunho: item Ativo=true, mas a matriz dele
    // está em Status=Rascunho, precisa ser rejeitado.
    [Fact]
    public async Task CriarAsync_ItemDeMatrizRascunho_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppcRascunho = new MatrizReferencia { Nome = "PPC em elaboração", CursoId = cursoA.Id, Tipo = TipoMatrizReferencia.PPC, Status = StatusMatrizReferencia.Rascunho };
        db.MatrizesReferencia.Add(ppcRascunho);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppcRascunho.Id); // Ativo = true
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloBase(assunto.Id);
        modelo.CursoContextoMatrizId = cursoA.Id;
        modelo.ItemMatrizIds = new List<int> { item.Id };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Rascunho", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Teste 7 do pedido — item inexistente: [existente, existente, 999999]
    // rejeita a operação INTEIRA (nem os dois que existem ficam salvos).
    [Fact]
    public async Task CriarAsync_ItemMatrizIdInexistenteJunto_RejeitaOperacaoInteira()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item1 = TestSeed.Item("C01", "Competência 1", ppc.Id);
        var item2 = TestSeed.Item("C02", "Competência 2", ppc.Id);
        db.ItensMatrizReferencia.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var idInexistente = item1.Id + item2.Id + 999_000; // garantidamente não existe no InMemory (ids sequenciais pequenos)

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloBase(assunto.Id);
        modelo.CursoContextoMatrizId = cursoA.Id;
        modelo.ItemMatrizIds = new List<int> { item1.Id, item2.Id, idInexistente };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("não existem", ex.Message);

        // Nenhuma questão foi criada — nem "salvou os dois que existem".
        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Teste 8 do pedido — IDs duplicados: [id, id] não deve ser tratado como
    // "inexistente" nem gerar erro de chave duplicada — normaliza pra um
    // Distinct e segue normalmente.
    [Fact]
    public async Task CriarAsync_ItemMatrizIdsDuplicados_EhNormalizadoENaoFalha()
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

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloBase(assunto.Id);
        modelo.CursoContextoMatrizId = cursoA.Id;
        modelo.ItemMatrizIds = new List<int> { item.Id, item.Id };

        var criada = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
    }

    // Teste 11 do pedido — histórico com item posteriormente inativado: a
    // questão JÁ tinha o item (ativo na hora de vincular); depois o item é
    // desativado (ex.: matriz descontinuada). Reeditar a questão SEM tocar
    // no alinhamento curricular (reenviando o mesmo ItemMatrizIds) não pode
    // ser bloqueado nem apagar o vínculo em silêncio.
    [Fact]
    public async Task AtualizarAsync_ItemJaVinculadoFicaInativoDepois_ResaveSemTocarPreservaOVinculo()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id); // Ativo = true no momento do vínculo
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // O item é desativado DEPOIS de já vinculado (ex.: matriz descontinuada).
        item.Ativo = false;
        await db.SaveChangesAsync();

        // Reedita a questão sem tocar no alinhamento curricular — o professor
        // só corrigiu o enunciado, por exemplo. modelo.ItemMatrizIds continua
        // mandando o MESMO id de antes, porque é isso que
        // QuestaoForm.razor faria (o item não sumiu do HashSet
        // itemMatrizIdsSelecionados só porque parou de ser OFERECIDO no
        // checklist).
        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.Enunciado = "Questão de teste (corrigida)";
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { item.Id };

        await service.AtualizarAsync(
            criada.Id,
            editarModelo,
            new List<QuestaoImagem>(),
            new HashSet<int>(),
            new List<PendenteImagem>(),
            meuId: "prof-1",
            minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
        Assert.Equal(item.Id, recarregada.ItensMatriz[0].Id);
        Assert.Equal("Questão de teste (corrigida)", recarregada.Enunciado);
    }

    // Teste 12 do pedido — mesmo princípio acima, mas pra matriz que virou
    // Rascunho depois do vínculo já existir (em vez do item individual ficar
    // inativo).
    [Fact]
    public async Task AtualizarAsync_MatrizJaVinculadaViraRascunhoDepois_ResaveSemTocarPreservaOVinculo()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC); // Status = Ativa no momento do vínculo
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // A matriz volta pra Rascunho depois (ex.: correção de um erro
        // percebido na edição publicada) — cenário raro, mas o princípio de
        // "não apagar histórico" precisa valer igual.
        ppc.Status = StatusMatrizReferencia.Rascunho;
        await db.SaveChangesAsync();

        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { item.Id };

        await service.AtualizarAsync(
            criada.Id,
            editarModelo,
            new List<QuestaoImagem>(),
            new HashSet<int>(),
            new List<PendenteImagem>(),
            meuId: "prof-1",
            minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
    }

    // Contraprova do carryover acima: a exceção de histórico só vale pro que
    // JÁ estava vinculado — se, na MESMA edição, o professor tentar
    // ADICIONAR um item novo que está inativo, isso continua rejeitado
    // normalmente (não é "resave", é associação nova).
    [Fact]
    public async Task AtualizarAsync_AdicionarItemInativoNovoJuntoComHistorico_EhRejeitado()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var itemHistorico = TestSeed.Item("C01", "Competência 1", ppc.Id);
        var itemNovoInativo = new ItemMatrizReferencia { Codigo = "C02", Titulo = "Competência 2", MatrizReferenciaId = ppc.Id, Ativo = false };
        db.ItensMatrizReferencia.AddRange(itemHistorico, itemNovoInativo);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);

        var service = NovoService(db);
        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { itemHistorico.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { itemHistorico.Id, itemNovoInativo.Id }; // adicionou um item inativo

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

    // Defesa em profundidade — MatrizReferenciaService.ValidarItensDoCursoAsync
    // isolado: item institucional inativo não deve ser devolvido, mesmo que
    // pertença ao curso certo.
    [Fact]
    public async Task ValidarItensDoCurso_ItemInativo_NaoEhDevolvido()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();

        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var itemInativo = new ItemMatrizReferencia { Codigo = "C01", Titulo = "Competência 1", MatrizReferenciaId = ppc.Id, Ativo = false };
        db.ItensMatrizReferencia.Add(itemInativo);
        await db.SaveChangesAsync();

        var matrizService = TestFakes.NovaMatrizReferenciaService(db);
        var aceitos = await matrizService.ValidarItensDoCursoAsync(cursoA.Id, new List<int> { itemInativo.Id });

        Assert.Empty(aceitos);
    }

    // Mesma ideia, pro lado nacional — ValidarItensPorAreasCursoAsync com
    // matriz Rascunho.
    [Fact]
    public async Task ValidarItensPorAreasCurso_MatrizRascunho_NaoEhDevolvido()
    {
        using var db = TestDbFactory.Criar();

        var area = TestSeed.AreaCurso("Engenharia de Computação");
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();

        var matrizRascunho = new MatrizReferencia { Nome = "ENADE 2026 (em preparação)", AreaCursoId = area.Id, Tipo = TipoMatrizReferencia.ENADE, Status = StatusMatrizReferencia.Rascunho };
        db.MatrizesReferencia.Add(matrizRascunho);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C08", "Competência 8", matrizRascunho.Id); // Ativo = true
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var matrizService = TestFakes.NovaMatrizReferenciaService(db);
        var aceitos = await matrizService.ValidarItensPorAreasCursoAsync(new List<int> { area.Id }, new List<int> { item.Id });

        Assert.Empty(aceitos);
    }
}
