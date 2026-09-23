using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa EstatisticaService: contagens do painel (ObterPainelAsync) e o mapa
// de cobertura por Assunto/Bloom (ObterCoberturaAsync). Questões seedadas
// como Compartilhada (padrão de TestSeed.Questao) pra não depender de
// ApplicationUser/Instituicao só pra passar em VisivelPara.
public class EstatisticaServiceTests
{
    [Fact]
    public async Task ObterPainelAsync_ContaQuestoesDisciplinasEAssuntos()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Cálculo I");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Limites", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        db.Questoes.AddRange(
            TestSeed.Questao("Q1", assunto.Id, Dificuldade.Facil),
            TestSeed.Questao("Q2", assunto.Id, Dificuldade.Media),
            TestSeed.Questao("Q3", assunto.Id, Dificuldade.Dificil));
        var inativa = TestSeed.Questao("Q4 inativa", assunto.Id);
        inativa.Ativa = false;
        db.Questoes.Add(inativa);
        await db.SaveChangesAsync();

        var service = new EstatisticaService(db);
        var painel = await service.ObterPainelAsync(null);

        Assert.Equal(3, painel.TotalQuestoesAtivas); // a inativa não conta
        Assert.Equal(1, painel.TotalDisciplinas);
        Assert.Equal(1, painel.TotalAssuntos);
    }

    [Fact]
    public async Task ObterPainelAsync_DistribuiPorDificuldadeETipo()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Física");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Cinemática", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        db.Questoes.AddRange(
            TestSeed.Questao("Fácil 1", assunto.Id, Dificuldade.Facil),
            TestSeed.Questao("Fácil 2", assunto.Id, Dificuldade.Facil),
            TestSeed.Questao("Média 1", assunto.Id, Dificuldade.Media));
        await db.SaveChangesAsync();

        var service = new EstatisticaService(db);
        var painel = await service.ObterPainelAsync(null);

        Assert.Equal(2, painel.PorDificuldade.Single(i => i.Rotulo == "Fácil").Quantidade);
        Assert.Equal(1, painel.PorDificuldade.Single(i => i.Rotulo == "Média").Quantidade);
        Assert.Equal(0, painel.PorDificuldade.Single(i => i.Rotulo == "Difícil").Quantidade);
        Assert.Equal(3, painel.PorTipo.Single(i => i.Rotulo == "Múltipla escolha").Quantidade);

        var disciplinaContagem = painel.PorDisciplina.Single();
        Assert.Equal("Física", disciplinaContagem.Rotulo);
        Assert.Equal(3, disciplinaContagem.Quantidade);
    }

    [Fact]
    public async Task ObterPainelAsync_ContaSoMinhasProvas()
    {
        using var db = TestDbFactory.Criar();
        db.Provas.AddRange(
            new Prova { Titulo = "Minha 1", CriadoPorId = "prof-1" },
            new Prova { Titulo = "Minha 2", CriadoPorId = "prof-1" },
            new Prova { Titulo = "De outro professor", CriadoPorId = "prof-2" });
        await db.SaveChangesAsync();

        var service = new EstatisticaService(db);
        var painel = await service.ObterPainelAsync("prof-1");

        Assert.Equal(2, painel.MinhasProvasTotal);
    }

    [Fact]
    public async Task ObterPainelAsync_ProvasEsteMesEProvasPorMes()
    {
        using var db = TestDbFactory.Criar();
        var agora = DateTime.UtcNow;
        db.Provas.AddRange(
            new Prova { Titulo = "Deste mês", CriadoPorId = "prof-1", CriadoEm = agora },
            new Prova { Titulo = "Mês passado", CriadoPorId = "prof-1", CriadoEm = agora.AddMonths(-1) },
            new Prova { Titulo = "Há um ano", CriadoPorId = "prof-1", CriadoEm = agora.AddYears(-1) });
        await db.SaveChangesAsync();

        var service = new EstatisticaService(db);
        var painel = await service.ObterPainelAsync("prof-1");

        Assert.Equal(1, painel.ProvasEsteMes);
        Assert.Equal(6, painel.ProvasPorMes.Count);
        Assert.Equal(2, painel.ProvasPorMes.Sum(i => i.Quantidade)); // "há um ano" fica fora da janela de 6 meses
    }

    [Fact]
    public async Task ObterCoberturaAsync_IncluiAssuntoSemQuestoesEClassificaPorBloom()
    {
        using var db = TestDbFactory.Criar();
        var disciplina = TestSeed.Disciplina("Cálculo I");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();

        var comQuestoes = TestSeed.Assunto("Limites", disciplina.Id);
        var semQuestoes = TestSeed.Assunto("Integrais", disciplina.Id);
        db.Assuntos.AddRange(comQuestoes, semQuestoes);
        await db.SaveChangesAsync();

        var q1 = TestSeed.Questao("Q1", comQuestoes.Id);
        q1.Bloom = NivelBloom.Lembrar;
        var q2 = TestSeed.Questao("Q2", comQuestoes.Id);
        q2.Bloom = NivelBloom.Aplicar;
        var q3 = TestSeed.Questao("Q3 sem bloom", comQuestoes.Id);
        db.Questoes.AddRange(q1, q2, q3);
        await db.SaveChangesAsync();

        var service = new EstatisticaService(db);
        var cobertura = await service.ObterCoberturaAsync(null);

        Assert.Equal(2, cobertura.Count);

        var linhaComQuestoes = cobertura.Single(c => c.Assunto == "Limites");
        Assert.Equal(3, linhaComQuestoes.Total);
        Assert.Equal(1, linhaComQuestoes.Lembrar);
        Assert.Equal(1, linhaComQuestoes.Aplicar);
        Assert.Equal(1, linhaComQuestoes.SemClassificacao);
        Assert.Equal(0, linhaComQuestoes.Entender);

        var linhaSemQuestoes = cobertura.Single(c => c.Assunto == "Integrais");
        Assert.Equal(0, linhaSemQuestoes.Total);
    }
}
