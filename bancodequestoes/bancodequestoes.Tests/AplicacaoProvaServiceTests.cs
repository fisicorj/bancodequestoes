using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa AplicacaoProvaService: validação de tipo suportado, geração de código único,
// e o ciclo Aberta/Encerrada.
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

        var aluno = new Aluno { Nome = "Maria" };
        db.Alunos.Add(aluno);
        await db.SaveChangesAsync();
        db.TurmasAlunos.Add(new TurmaAluno { TurmaId = turma.Id, AlunoId = aluno.Id, Ativa = true });
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
    public async Task CriarAsync_TipoSuportado_GeraUmAcessoPorAlunoMatriculadoComCodigoDe6Caracteres()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);

        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var acessos = await servico.ListarAcessosAsync(aplicacao.Id);

        Assert.Single(acessos);
        Assert.Equal(6, acessos[0].CodigoAcesso.Length);
        Assert.Equal(StatusAplicacaoProva.Aberta, aplicacao.Status);
        Assert.Equal("prof-1", aplicacao.CriadoPorId);
    }

    [Fact]
    public async Task CriarAsync_TurmaSemAlunoMatriculadoAtivo_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var turma = TestSeed.Turma("EC1B", curso.Id, disciplina.Id);
        db.Turmas.Add(turma);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        var questao = TestSeed.Questao("Q1", assunto.Id);
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();
        var prova = new Prova { Titulo = "Prova 1" };
        prova.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0 });
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var servico = new AplicacaoProvaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1"));
    }

    [Fact]
    public async Task GerarAcessoAsync_ChamadoDuasVezes_NaoDuplicaCodigo()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);
        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var acessoExistente = (await servico.ListarAcessosAsync(aplicacao.Id))[0];

        var primeiro = await servico.GerarAcessoAsync(aplicacao.Id, acessoExistente.AlunoId);
        var segundo = await servico.GerarAcessoAsync(aplicacao.Id, acessoExistente.AlunoId);

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Equal(primeiro.CodigoAcesso, segundo.CodigoAcesso);
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
    public async Task ObterAcessoPorCodigoAsync_CodigoInexistente_RetornaNulo()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AplicacaoProvaService(db);

        Assert.Null(await servico.ObterAcessoPorCodigoAsync("XXXXXX"));
    }

    [Fact]
    public async Task ExcluirAsync_SemTentativaDeAluno_ExcluiApesarDosCodigosDeAcessoJaGerados()
    {
        // AcessoAlunoAplicacao é Cascade (não Restrict) — diferente de RespostaProvaOnline
        // abaixo — porque o código em si não é dado do aluno, só é gerado automaticamente
        // pra todo mundo matriculado na hora de criar a aplicação. Excluir precisa
        // funcionar mesmo com os códigos já tracked neste mesmo DbContext (CriarAsync
        // acabou de criá-los), senão NENHUMA aplicação seria excluível.
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);
        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");

        await servico.ExcluirAsync(aplicacao);

        Assert.Null(await db.AplicacoesProva.FindAsync(aplicacao.Id));
    }

    [Fact]
    public async Task ExcluirAsync_ComTentativaDeAlunoJaRastreadaNoContexto_Lanca()
    {
        // Simula o cenário real que causava InvalidOperationException (relação
        // "severed") em vez da mensagem amigável: a RespostaProvaOnline já está tracked
        // neste DbContext (ex.: o usuário abriu a tela de resultados antes de excluir).
        using var db = TestDbFactory.Criar();
        var (prova, turma) = await SeedProvaETurmaAsync(db);
        var servico = new AplicacaoProvaService(db);
        var aplicacao = await servico.CriarAsync(new AplicacaoProvaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var acesso = (await servico.ListarAcessosAsync(aplicacao.Id))[0];

        var respostaServico = new RespostaProvaOnlineService(db);
        await respostaServico.IniciarOuRetomarAsync(aplicacao.Id, acesso.AlunoId);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.ExcluirAsync(aplicacao));
    }
}
