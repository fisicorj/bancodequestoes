using BancoQuestoes.Data;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "Implementar suporte estruturado a questões ENADE e
// Formação Geral" — cobre os 9 cenários pedidos (itens 38-46) mais algumas
// checagens de coerência extras que a validação acrescentou por conta
// própria (ver comentário em QuestaoCurricularService.ValidarConsistenciaEnadeAsync).
//
// Fora do escopo destes testes (mesma ressalva de sempre neste projeto —
// ver AjustesFinaisIntegridadeCurricularTests.cs): comportamento só de UI
// (QuestaoMetadadosSection.razor — mostrar/esconder campos, sugestão
// automática de Disciplina, avisos) não é coberto aqui, não há bUnit no
// repositório; verificado por leitura de código, documentado como pendência
// real no relatório final.
public class SuporteEnadeFormacaoGeralTests
{
    private static QuestaoService NovoService(ApplicationDbContext db) =>
        TestFakes.NovoQuestaoService(db);

    // Disciplina "Formação Geral" de verdade (mesmo mecanismo idempotente
    // usado em produção — ver Program.cs/DbSeeder.SeedDisciplinaFormacaoGeralAsync),
    // não uma Disciplina qualquer chamada "Formação Geral" à mão — garante
    // que o teste valida a mesma checagem (Disciplina.Codigo) que roda de
    // verdade, não só uma coincidência de Nome.
    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedFormacaoGeralAsync(ApplicationDbContext db)
    {
        await DbSeeder.SeedDisciplinaFormacaoGeralAsync(db);
        var disciplina = await db.Disciplinas.Include(d => d.Assuntos).FirstAsync(d => d.Codigo == Disciplina.CodigoFormacaoGeral);
        var assunto = disciplina.Assuntos.First(a => a.Nome == "Conhecimentos Gerais");
        return (disciplina, assunto);
    }

    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedDisciplinaComumAsync(ApplicationDbContext db, string nome = "Circuitos Elétricos")
    {
        var disciplina = TestSeed.Disciplina(nome);
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Assunto de teste", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return (disciplina, assunto);
    }

    private static async Task<AreaCurso> SeedAreaCursoAsync(ApplicationDbContext db, string nome = "Engenharia de Computação")
    {
        var area = TestSeed.AreaCurso(nome);
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();
        return area;
    }

    private static QuestaoInput ModeloMultiplaEscolha(int assuntoId) => new()
    {
        AssuntoId = assuntoId,
        Enunciado = "Questão de teste ENADE",
        Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
    };

    private static QuestaoInput ModeloDiscursiva(int assuntoId) => new()
    {
        AssuntoId = assuntoId,
        TipoQuestao = TipoQuestao.Discursiva,
        Enunciado = "Questão discursiva de teste ENADE",
        RespostaEsperada = "Resposta esperada de teste.",
    };

    // Cenário (a): Formação Geral válida — Origem=Enade, Secao=FormacaoGeral,
    // Disciplina=Formação Geral, sem nenhuma AreaCurso -> deve ser aceita.
    [Fact]
    public async Task CriarAsync_FormacaoGeral_SemAreaCurso_EhAceita()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedFormacaoGeralAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.FormacaoGeral;
        modelo.Ano = 2023;
        modelo.NumeroOriginal = "04";

        var questao = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(SecaoEnade.FormacaoGeral, questao.SecaoEnade);
        Assert.Empty(questao.AreasCurso);
        Assert.Equal(1, await db.Questoes.CountAsync());
    }

    // Cenário (b): Formação Geral com Disciplina ERRADA (qualquer uma que
    // não seja a "Formação Geral" de Codigo estável) -> deve ser rejeitada.
    [Fact]
    public async Task CriarAsync_FormacaoGeral_ComDisciplinaErrada_EhRejeitada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assuntoErrado) = await SeedDisciplinaComumAsync(db, "Matemática");

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assuntoErrado.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.FormacaoGeral;

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Formação Geral", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Cenário (c): Componente Específico com AreaCurso e QUALQUER Disciplina
    // (não precisa ser Formação Geral nem nenhuma disciplina específica) ->
    // deve ser aceita.
    [Fact]
    public async Task CriarAsync_ComponenteEspecifico_ComAreaCurso_QualquerDisciplina_EhAceita()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db, "Circuitos Elétricos");
        var area = await SeedAreaCursoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.ComponenteEspecifico;
        modelo.AreaCursoIds = new List<int> { area.Id };
        modelo.Ano = 2023;
        modelo.NumeroOriginal = "13";

        var questao = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(SecaoEnade.ComponenteEspecifico, questao.SecaoEnade);
        Assert.Single(questao.AreasCurso);
        Assert.Equal(1, await db.Questoes.CountAsync());
    }

    // Cenário (d): Componente Específico SEM nenhuma AreaCurso -> rejeitada.
    [Fact]
    public async Task CriarAsync_ComponenteEspecifico_SemAreaCurso_EhRejeitada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.ComponenteEspecifico;

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Área de Curso", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Cenário (e): Origem=Enade sem SecaoEnade nenhum -> rejeitada.
    [Fact]
    public async Task CriarAsync_Enade_SemSecaoEnade_EhRejeitada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = null;

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Seção", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Cenário (f): Origem=Autoral (não-ENADE) sem SecaoEnade -> aceita
    // normalmente (SecaoEnade nunca é obrigatório fora do ENADE).
    [Fact]
    public async Task CriarAsync_Autoral_SemSecaoEnade_EhAceita()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Autoral;
        modelo.SecaoEnade = null;

        var questao = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Null(questao.SecaoEnade);
        Assert.Equal(1, await db.Questoes.CountAsync());
    }

    // Checagem extra (não um dos 9 cenários pedidos, mas decorre diretamente
    // do item "não confundir Origem com categorização pedagógica" e do "SecaoEnade
    // só é relevante pra Origem=ENADE" — ver comentário em
    // ValidarConsistenciaEnadeAsync): Origem != Enade só é aceito com
    // SecaoEnade nulo; se vier preenchido mesmo assim, rejeita.
    [Fact]
    public async Task CriarAsync_OrigemNaoEnade_ComSecaoEnadePreenchida_EhRejeitada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Autoral;
        modelo.SecaoEnade = SecaoEnade.ComponenteEspecifico;

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("ENADE", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Checagem extra: Formação Geral com AreaCurso marcada -> rejeitada
    // ("sem nunca misturar Formação Geral com AreaCurso", item final do
    // pedido) — Disciplina certa não basta se ainda tiver Área vinculada.
    [Fact]
    public async Task CriarAsync_FormacaoGeral_ComAreaCurso_EhRejeitada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.FormacaoGeral;
        modelo.AreaCursoIds = new List<int> { area.Id };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Formação Geral", ex.Message);

        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    // Cenário (g): NumeroOriginal aceita rótulo não-numérico de questão
    // discursiva ("D1") — item "NumeroOriginal deve ser string, não int"
    // (item 18 do pedido), verificado com uma questão Discursiva de verdade
    // (Componente Específico, pra também exercitar o caminho AreaCurso).
    [Fact]
    public async Task CriarAsync_ComponenteEspecifico_Discursiva_NumeroOriginalD1_EhAceita()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedDisciplinaComumAsync(db);
        var area = await SeedAreaCursoAsync(db);

        var service = NovoService(db);
        var modelo = ModeloDiscursiva(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.ComponenteEspecifico;
        modelo.AreaCursoIds = new List<int> { area.Id };
        modelo.Ano = 2023;
        modelo.NumeroOriginal = "D1";

        var questao = await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal("D1", questao.NumeroOriginal);
        Assert.Equal(1, await db.Questoes.CountAsync());
    }

    // Cenário (h): duplicata simulada de Formação Geral 2023 Q04 — o helper
    // de chave natural (QuestaoEnadeChaveNatural, preparado pro futuro
    // importador) precisa achar a questão já cadastrada.
    [Fact]
    public async Task EnadeChaveNatural_FormacaoGeral_DetectaDuplicataSimulada()
    {
        using var db = TestDbFactory.Criar();
        var (_, assunto) = await SeedFormacaoGeralAsync(db);

        var service = NovoService(db);
        var modelo = ModeloMultiplaEscolha(assunto.Id);
        modelo.Origem = OrigemQuestao.Enade;
        modelo.SecaoEnade = SecaoEnade.FormacaoGeral;
        modelo.Ano = 2023;
        modelo.NumeroOriginal = "04";
        await service.CriarAsync(modelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // "Importação" simulada da MESMA questão (mesmo Ano/Seção/Número) —
        // o helper deve encontrar a que já existe, sem precisar de nenhuma
        // AreaCurso (Formação Geral nunca tem).
        var duplicata = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, ano: 2023, secao: SecaoEnade.FormacaoGeral, numeroOriginal: "04", areaCursoId: null);

        Assert.NotNull(duplicata);

        // Um ano ou número diferente não deve ser encontrado como duplicata.
        var naoDuplicataAno = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, ano: 2024, secao: SecaoEnade.FormacaoGeral, numeroOriginal: "04", areaCursoId: null);
        Assert.Null(naoDuplicataAno);

        var naoDuplicataNumero = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, ano: 2023, secao: SecaoEnade.FormacaoGeral, numeroOriginal: "05", areaCursoId: null);
        Assert.Null(naoDuplicataNumero);
    }

    // Cenário (i): "Questão 13" de Componente Específico em DUAS Áreas de
    // Curso diferentes NUNCA pode ser tratada como a mesma questão — o
    // helper de chave natural precisa considerar a Área, não só Ano+Número.
    [Fact]
    public async Task EnadeChaveNatural_ComponenteEspecifico_MesmoNumeroAreasDiferentes_NaoEhTratadoComoDuplicata()
    {
        using var db = TestDbFactory.Criar();
        var (_, assuntoEngComp) = await SeedDisciplinaComumAsync(db, "Circuitos Elétricos");
        var (_, assuntoCienciaComp) = await SeedDisciplinaComumAsync(db, "Estrutura de Dados");
        var areaEngenharia = await SeedAreaCursoAsync(db, "Engenharia de Computação");
        var areaCiencia = await SeedAreaCursoAsync(db, "Ciência da Computação");

        var service = NovoService(db);

        var modeloEngenharia = ModeloMultiplaEscolha(assuntoEngComp.Id);
        modeloEngenharia.Origem = OrigemQuestao.Enade;
        modeloEngenharia.SecaoEnade = SecaoEnade.ComponenteEspecifico;
        modeloEngenharia.AreaCursoIds = new List<int> { areaEngenharia.Id };
        modeloEngenharia.Ano = 2023;
        modeloEngenharia.NumeroOriginal = "13";
        await service.CriarAsync(modeloEngenharia, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // Mesmo Ano/Seção/Número, Área DIFERENTE — não deve encontrar a
        // questão de Engenharia como duplicata.
        var duplicataNaOutraArea = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, ano: 2023, secao: SecaoEnade.ComponenteEspecifico, numeroOriginal: "13", areaCursoId: areaCiencia.Id);
        Assert.Null(duplicataNaOutraArea);

        // Mesma Área -> aí sim encontra.
        var duplicataNaMesmaArea = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, ano: 2023, secao: SecaoEnade.ComponenteEspecifico, numeroOriginal: "13", areaCursoId: areaEngenharia.Id);
        Assert.NotNull(duplicataNaMesmaArea);

        // As duas questões de Área diferente com o mesmo número continuam
        // podendo coexistir no banco sem conflito nenhum (nunca uma
        // sobrescreve/impede a outra).
        var modeloCiencia = ModeloMultiplaEscolha(assuntoCienciaComp.Id);
        modeloCiencia.Origem = OrigemQuestao.Enade;
        modeloCiencia.SecaoEnade = SecaoEnade.ComponenteEspecifico;
        modeloCiencia.AreaCursoIds = new List<int> { areaCiencia.Id };
        modeloCiencia.Ano = 2023;
        modeloCiencia.NumeroOriginal = "13";
        await service.CriarAsync(modeloCiencia, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(2, await db.Questoes.CountAsync());
    }
}
