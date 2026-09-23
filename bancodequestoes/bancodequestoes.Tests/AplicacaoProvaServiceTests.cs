using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa AplicacaoProvaService: validação de tipo suportado, geração de código único,
// e o ciclo Aberta/Encerrada. Não cobre o catch de DbUpdateException em ExcluirAsync — o
// provider InMemory não aplica a mesma restrição de FK (Restrict) do Postgres real.
public class AplicacaoProvaServiceTests
{
    private static async Task<(Prova Prova, Turma Turma)> SeedProvaETurmaAsync(BancoQuestoes.Data.ApplicationDbContext db, TipoQuestao tipo = TipoQuestao.MultiplaEscolha)
    {
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();

        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();

        var turma = TestSeed.Turma("EC1A", curso.Id, disciplina.Id);
        db.Turmas.Add(turma);
        await db.SaveChangesAsync();

        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();

        Questao questao = tipo switch
        {
            TipoQuestao.Discursiva => new QuestaoDiscursiva { Enunciado = "Discorra sobre X", AssuntoId = assunto.Id, TipoQuestao = tipo, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true, RespostaEsperada = "Resposta esperada" },
            _ => TestSeed.Questao("Q1", assunto.Id, tipo: tipo),
        };
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var prova = new Prova { Titulo = "Prova 1" };
        prova.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0 });
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        return (prova, turma);
    }

    [Fact]
    public async Task CriarAsync_ProvaComTipoNaoSuportado_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db, TipoQuestao.Discursiva);
        var servico = new AplicacaoProvaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1"));
    }

    [Fact]
    public async Task CriarAsync_SemProvaSelecionada_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AplicacaoProvaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new AplicacaoProvaInput { ProvaId = 0, TurmaId = 1 }, "prof-1"));
    }

    [Fact]
    public async Task CriarAsync_TipoSuportado_GeraCodigoUnicoDe6Caracteres()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);

        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");

        Assert.Equal(6, aplicacao.CodigoAcesso.Length);
        Assert.Equal(StatusAplicacaoProva.Aberta, aplicacao.Status);
        Assert.Equal("prof-1", aplicacao.CriadoPorId);
    }

    [Fact]
    public async Task AlternarStatusAsync_AlternaEntreAbertaEEncerrada()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);
        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");

        await servico.AlternarStatusAsync(aplicacao);
        Assert.Equal(StatusAplicacaoProva.Encerrada, aplicacao.Status);

        await servico.AlternarStatusAsync(aplicacao);
        Assert.Equal(StatusAplicacaoProva.Aberta, aplicacao.Status);
    }

    [Fact]
    public async Task ObterPorCodigoAsync_CodigoInexistente_RetornaNulo()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AplicacaoProvaService(db);

        Assert.Null(await servico.ObterPorCodigoAsync("XXXXXX"));
    }
}
