using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa RespostaProvaOnlineService: fluxo do aluno (iniciar/retomar/enviar), a regra de
// tentativa única, e a correção automática dos 4 tipos suportados (inclusive Lacunas
// "tudo ou nada"). Não cobre FK Restrict/Cascade — mesma limitação do InMemory já
// documentada em TestDbFactory.
public class RespostaProvaOnlineServiceTests
{
    // Monta Curso/Turma/Aluno matriculado + AplicacaoProva Aberta pra uma questão de um
    // tipo só — a maioria dos testes de correção só precisa de UMA questão.
    private static async Task<(AplicacaoProva Aplicacao, Aluno Aluno, Questao Questao)> SeedCenarioAsync(
        BancoQuestoes.Data.ApplicationDbContext db, Questao questao)
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

        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var prova = new Prova { Titulo = "Prova 1" };
        prova.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0, Valor = 10m });
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var aplicacao = new AplicacaoProva { ProvaId = prova.Id, TurmaId = turma.Id, CodigoAcesso = "ABC123" };
        db.AplicacoesProva.Add(aplicacao);
        await db.SaveChangesAsync();

        return (aplicacao, aluno, questao);
    }

    private static async Task<Assunto> SeedAssuntoAsync(BancoQuestoes.Data.ApplicationDbContext db)
    {
        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return assunto;
    }

    [Fact]
    public async Task IniciarOuRetomarAsync_AplicacaoEncerrada_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        aplicacao.Status = StatusAplicacaoProva.Encerrada;
        await db.SaveChangesAsync();

        var servico = new RespostaProvaOnlineService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id));
    }

    [Fact]
    public async Task IniciarOuRetomarAsync_AlunoNaoMatriculado_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, _, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var outroAluno = new Aluno { Nome = "Outro" };
        db.Alunos.Add(outroAluno);
        await db.SaveChangesAsync();

        var servico = new RespostaProvaOnlineService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.IniciarOuRetomarAsync(aplicacao.Id, outroAluno.Id));
    }

    [Fact]
    public async Task IniciarOuRetomarAsync_PrimeiraVez_CriaTentativaComUmaRespostaPorQuestao()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, questao) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);

        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        Assert.Equal(StatusRespostaProvaOnline.EmAndamento, tentativa.Status);
        Assert.Single(tentativa.Respostas);
        Assert.Equal(questao.Id, tentativa.Respostas[0].QuestaoId);
    }

    [Fact]
    public async Task IniciarOuRetomarAsync_ChamadoDeNovoEmAndamento_RetomaMesmaTentativa()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);

        var primeira = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);
        var segunda = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        Assert.Equal(primeira.Id, segunda.Id);
    }

    [Fact]
    public async Task IniciarOuRetomarAsync_TentativaJaEnviada_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);

        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);
        await servico.EnviarAsync(tentativa.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id));
    }

    [Fact]
    public async Task EnviarAsync_MultiplaEscolhaCorreta_PontuaTotal()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id)); // RespostaCorreta = 'A'
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarMultiplaEscolhaAsync(tentativa.Respostas[0].Id, 'A');
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.Equal(StatusRespostaProvaOnline.Enviada, enviada.Status);
        Assert.Equal(10m, enviada.NotaTotal);
        Assert.True(enviada.Respostas[0].Correta);
    }

    [Fact]
    public async Task EnviarAsync_MultiplaEscolhaErrada_ZeraPontuacao()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarMultiplaEscolhaAsync(tentativa.Respostas[0].Id, 'B');
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.Equal(0m, enviada.NotaTotal);
        Assert.False(enviada.Respostas[0].Correta);
    }

    [Fact]
    public async Task EnviarAsync_CertoErrado_Corrige()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var questao = new QuestaoCertoErrado { Enunciado = "Afirmação", AssuntoId = assunto.Id, TipoQuestao = TipoQuestao.CertoErrado, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true, RespostaCorreta = true };
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, questao);
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarCertoErradoAsync(tentativa.Respostas[0].Id, true);
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.True(enviada.Respostas[0].Correta);
        Assert.Equal(10m, enviada.NotaTotal);
    }

    [Fact]
    public async Task EnviarAsync_Numerica_DentroDaToleranciaAcerta()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var questao = new QuestaoNumerica { Enunciado = "Calcule", AssuntoId = assunto.Id, TipoQuestao = TipoQuestao.Numerica, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true, RespostaEsperada = 10m, Tolerancia = 0.5m };
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, questao);
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarNumericaAsync(tentativa.Respostas[0].Id, 10.3m);
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.True(enviada.Respostas[0].Correta);
    }

    [Fact]
    public async Task EnviarAsync_Numerica_ForaDaToleranciaErra()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var questao = new QuestaoNumerica { Enunciado = "Calcule", AssuntoId = assunto.Id, TipoQuestao = TipoQuestao.Numerica, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true, RespostaEsperada = 10m, Tolerancia = 0.5m };
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, questao);
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarNumericaAsync(tentativa.Respostas[0].Id, 11m);
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.False(enviada.Respostas[0].Correta);
    }

    [Fact]
    public async Task EnviarAsync_Lacunas_TodasCorretasAcerta()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var questao = new QuestaoLacunas { Enunciado = "O ___ é ___.", AssuntoId = assunto.Id, TipoQuestao = TipoQuestao.Lacunas, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true };
        questao.Lacunas.Add(new LacunaResposta { Ordem = 0, RespostaEsperada = "céu" });
        questao.Lacunas.Add(new LacunaResposta { Ordem = 1, RespostaEsperada = "azul" });
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, questao);
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarLacunasAsync(tentativa.Respostas[0].Id, new Dictionary<int, string> { [0] = "Céu", [1] = " AZUL " });
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.True(enviada.Respostas[0].Correta);
        Assert.Equal(10m, enviada.NotaTotal);
    }

    [Fact]
    public async Task EnviarAsync_Lacunas_UmaErradaZeraQuestaoInteira()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var questao = new QuestaoLacunas { Enunciado = "O ___ é ___.", AssuntoId = assunto.Id, TipoQuestao = TipoQuestao.Lacunas, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true };
        questao.Lacunas.Add(new LacunaResposta { Ordem = 0, RespostaEsperada = "céu" });
        questao.Lacunas.Add(new LacunaResposta { Ordem = 1, RespostaEsperada = "azul" });
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, questao);
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.SalvarLacunasAsync(tentativa.Respostas[0].Id, new Dictionary<int, string> { [0] = "céu", [1] = "verde" });
        var enviada = await servico.EnviarAsync(tentativa.Id);

        Assert.False(enviada.Respostas[0].Correta);
        Assert.Equal(0m, enviada.NotaTotal);
    }

    [Fact]
    public async Task EnviarAsync_ChamadoDuasVezes_SegundaLanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await servico.EnviarAsync(tentativa.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.EnviarAsync(tentativa.Id));
    }

    [Fact]
    public async Task EnviarAsync_ComMotivoForcado_GravaMotivoEncerramento()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        var enviada = await servico.EnviarAsync(tentativa.Id, MotivoEncerramento.PerdaDeFoco);

        Assert.Equal(MotivoEncerramento.PerdaDeFoco, enviada.MotivoEncerramento);
    }

    [Fact]
    public async Task SalvarMultiplaEscolhaAsync_TentativaJaEnviada_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);
        var respostaId = tentativa.Respostas[0].Id;
        await servico.EnviarAsync(tentativa.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.SalvarMultiplaEscolhaAsync(respostaId, 'A'));
    }

    [Fact]
    public async Task LiberarAsync_TentativaEmAndamento_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.LiberarAsync(tentativa));
    }

    [Fact]
    public async Task LiberarAsync_TentativaEnviada_Libera()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);
        var enviada = await servico.EnviarAsync(tentativa.Id);

        await servico.LiberarAsync(enviada);

        Assert.Equal(StatusRespostaProvaOnline.Liberada, enviada.Status);
        Assert.NotNull(enviada.LiberadoEm);
    }

    [Fact]
    public async Task ObterQuestoesParaExibicaoAsync_MultiplaEscolha_NaoExpoeGabarito()
    {
        using var db = TestDbFactory.Criar();
        var assunto = await SeedAssuntoAsync(db);
        var (aplicacao, aluno, _) = await SeedCenarioAsync(db, TestSeed.Questao("Q1", assunto.Id));
        var servico = new RespostaProvaOnlineService(db);
        var tentativa = await servico.IniciarOuRetomarAsync(aplicacao.Id, aluno.Id);

        var questoes = await servico.ObterQuestoesParaExibicaoAsync(tentativa.Id);

        Assert.Single(questoes);
        // O DTO de exibição não tem campo nenhum de gabarito — só chegar até aqui sem
        // erro de compilação já garante que RespostaCorreta/RespostaEsperada não vazam.
        Assert.Equal(TipoQuestao.MultiplaEscolha, questoes[0].Tipo);
    }
}
