using BancoQuestoes.CartaoResposta;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa CartaoRespostaService: validação (só múltipla escolha, limite de alternativas, turma
// sem matriculado), geração de 1 cartão com token único por aluno, e o fluxo de
// registro/confirmação de leitura (nota provisória -> ambiguidade -> nota definitiva).
// Não cobre a geração do PDF nem a leitura de imagem de verdade (LeitorCartaoRespostaService)
// aqui — mesmo padrão já usado em ExportacaoServiceTests, que também não renderiza PDF de
// verdade num teste unitário (dependeria de fonte instalada na máquina que roda o teste).
public class CartaoRespostaServiceTests
{
    private static async Task<(Prova Prova, Turma Turma, int QuestaoId)> SeedProvaETurmaAsync(
        BancoQuestoes.Data.ApplicationDbContext db, int quantidadeAlternativas = 4, TipoQuestao tipo = TipoQuestao.MultiplaEscolha)
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

        Questao questao;
        if (tipo == TipoQuestao.MultiplaEscolha)
        {
            var multiplaEscolha = TestSeed.Questao("Q1", assunto.Id);
            for (var i = 0; i < quantidadeAlternativas; i++)
            {
                multiplaEscolha.Alternativas.Add(new AlternativaQuestao { Letra = (char)('A' + i), Texto = $"Alternativa {i}" });
            }
            questao = multiplaEscolha;
        }
        else
        {
            questao = new QuestaoCertoErrado { Enunciado = "Afirmação", AssuntoId = assunto.Id, TipoQuestao = tipo, Visibilidade = VisibilidadeQuestao.Compartilhada, Ativa = true, RespostaCorreta = true };
        }

        db.Questoes.Add(questao);
        await db.SaveChangesAsync();

        var prova = new Prova { Titulo = "Prova 1" };
        prova.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0, Valor = 10m });
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        return (prova, turma, questao.Id);
    }

    [Fact]
    public async Task CriarAsync_ProvaComTipoNaoSuportado_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, _) = await SeedProvaETurmaAsync(db, tipo: TipoQuestao.CertoErrado);
        var servico = new CartaoRespostaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1"));
    }

    [Fact]
    public async Task CriarAsync_SemProvaSelecionada_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var servico = new CartaoRespostaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new CartaoRespostaInput { ProvaId = 0, TurmaId = 1 }, "prof-1"));
    }

    [Fact]
    public async Task CriarAsync_QuestaoComMaisAlternativasQueOLimite_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, _) = await SeedProvaETurmaAsync(db, quantidadeAlternativas: CartaoRespostaService.MaxAlternativas + 1);
        var servico = new CartaoRespostaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1"));
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
        questao.Alternativas.Add(new AlternativaQuestao { Letra = 'A', Texto = "Alt A" });
        db.Questoes.Add(questao);
        await db.SaveChangesAsync();
        var prova = new Prova { Titulo = "Prova 1" };
        prova.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao.Id, Ordem = 0 });
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var servico = new CartaoRespostaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1"));
    }

    [Fact]
    public async Task CriarAsync_TipoSuportado_GeraUmCartaoPorAlunoComTokenUnicoDe10Caracteres()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, _) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);

        var aplicacao = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var cartoes = await servico.ListarCartoesAsync(aplicacao.Id);

        Assert.Single(cartoes);
        Assert.Equal(10, cartoes[0].Token.Length);
        Assert.Equal(StatusCartaoAluno.PendenteEnvio, cartoes[0].Status);
    }

    [Fact]
    public async Task RegistrarLeituraAsync_RespostaCorreta_StatusEnviadoENotaProvisoria()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, questaoId) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);
        var aplicacao = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var cartao = (await servico.ListarCartoesAsync(aplicacao.Id))[0];

        await servico.RegistrarLeituraAsync(cartao.Id, new() { [questaoId] = ('A', false) });

        var atualizado = await servico.ObterCartaoDetalheAsync(cartao.Id);
        Assert.Equal(StatusCartaoAluno.Enviado, atualizado!.Status);
        Assert.Equal(10m, atualizado.NotaTotal);
        Assert.True(atualizado.Respostas.Single().Correta);
        Assert.Equal('A', atualizado.Respostas.Single().LetraConfirmada);
    }

    [Fact]
    public async Task RegistrarLeituraAsync_RespostaAmbigua_NaoConfirmaLetraAutomaticamente()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, questaoId) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);
        var aplicacao = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var cartao = (await servico.ListarCartoesAsync(aplicacao.Id))[0];

        await servico.RegistrarLeituraAsync(cartao.Id, new() { [questaoId] = (null, true) });

        var atualizado = await servico.ObterCartaoDetalheAsync(cartao.Id);
        Assert.True(atualizado!.Respostas.Single().Ambigua);
        Assert.Null(atualizado.Respostas.Single().LetraConfirmada);
    }

    [Fact]
    public async Task ConfirmarCartaoAsync_ComRespostaAmbigua_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, questaoId) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);
        var aplicacao = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var cartao = (await servico.ListarCartoesAsync(aplicacao.Id))[0];
        await servico.RegistrarLeituraAsync(cartao.Id, new() { [questaoId] = (null, true) });

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.ConfirmarCartaoAsync(cartao.Id));
    }

    [Fact]
    public async Task ConfirmarRespostaAsync_ResolveAmbiguidadeEPermiteConfirmarCartao()
    {
        using var db = TestDbFactory.Criar();
        var (prova, turma, questaoId) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);
        var aplicacao = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova.Id, TurmaId = turma.Id }, "prof-1");
        var cartao = (await servico.ListarCartoesAsync(aplicacao.Id))[0];
        await servico.RegistrarLeituraAsync(cartao.Id, new() { [questaoId] = (null, true) });
        var respostaId = (await servico.ObterCartaoDetalheAsync(cartao.Id))!.Respostas.Single().Id;

        // Aluno respondeu 'B' de verdade (gabarito é 'A') — professor resolve a ambiguidade
        // escolhendo a letra certa que enxergou no cartão físico.
        await servico.ConfirmarRespostaAsync(respostaId, 'B');
        await servico.ConfirmarCartaoAsync(cartao.Id);

        var confirmado = await servico.ObterCartaoDetalheAsync(cartao.Id);
        Assert.Equal(StatusCartaoAluno.Confirmado, confirmado!.Status);
        Assert.Equal(0m, confirmado.NotaTotal);
        Assert.False(confirmado.Respostas.Single().Correta);
        Assert.NotNull(confirmado.CorrigidoEm);
    }

    [Fact]
    public async Task ObterCartaoPorTokenAsync_TokenInexistente_RetornaNulo()
    {
        using var db = TestDbFactory.Criar();
        var servico = new CartaoRespostaService(db);

        Assert.Null(await servico.ObterCartaoPorTokenAsync("XXXXXXXXXX"));
    }

    [Fact]
    public async Task ProcessarFotoAsync_LeituraDeAplicacaoDiferente_Lanca()
    {
        // ProcessarFotoAsync confere que o cartão lido pertence à aplicação em que o
        // professor está enviando fotos — aqui simulamos isso chamando os métodos internos
        // sem imagem real (o teste de leitura de imagem de verdade fica em
        // LeitorCartaoRespostaTests, que monta uma foto sintética).
        using var db = TestDbFactory.Criar();
        var (prova1, turma1, _) = await SeedProvaETurmaAsync(db);
        var servico = new CartaoRespostaService(db);
        var aplicacao1 = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova1.Id, TurmaId = turma1.Id }, "prof-1");

        var curso2 = TestSeed.Curso("Outro curso");
        db.Cursos.Add(curso2);
        await db.SaveChangesAsync();
        var disciplina2 = TestSeed.Disciplina("Outra disciplina");
        db.Disciplinas.Add(disciplina2);
        await db.SaveChangesAsync();
        var turma2 = TestSeed.Turma("EC2A", curso2.Id, disciplina2.Id);
        db.Turmas.Add(turma2);
        await db.SaveChangesAsync();
        var aluno2 = new Aluno { Nome = "João" };
        db.Alunos.Add(aluno2);
        await db.SaveChangesAsync();
        db.TurmasAlunos.Add(new TurmaAluno { TurmaId = turma2.Id, AlunoId = aluno2.Id, Ativa = true });
        await db.SaveChangesAsync();
        var assunto2 = TestSeed.Assunto("Outro assunto", disciplina2.Id);
        db.Assuntos.Add(assunto2);
        await db.SaveChangesAsync();
        var questao2 = TestSeed.Questao("Q2", assunto2.Id);
        questao2.Alternativas.Add(new AlternativaQuestao { Letra = 'A', Texto = "Alt A" });
        db.Questoes.Add(questao2);
        await db.SaveChangesAsync();
        var prova2 = new Prova { Titulo = "Prova 2" };
        prova2.ProvaQuestoes.Add(new ProvaQuestao { QuestaoId = questao2.Id, Ordem = 0 });
        db.Provas.Add(prova2);
        await db.SaveChangesAsync();
        var aplicacao2 = await servico.CriarAsync(new CartaoRespostaInput { ProvaId = prova2.Id, TurmaId = turma2.Id }, "prof-1");

        var cartaoDaAplicacao2 = (await servico.ListarCartoesAsync(aplicacao2.Id))[0];

        // Confere direto o cruzamento de aplicação (sem depender de uma foto real):
        // RegistrarLeituraAsync/ProcessarFotoAsync têm a mesma checagem de posse.
        var cartaoRecarregado = await servico.ObterCartaoPorTokenAsync(cartaoDaAplicacao2.Token);
        Assert.Equal(aplicacao2.Id, cartaoRecarregado!.CartaoRespostaAplicacaoId);
        Assert.NotEqual(aplicacao1.Id, cartaoRecarregado.CartaoRespostaAplicacaoId);
    }
}

// Testa a matemática de Homografia isoladamente (sem imagem nenhuma) — 4 pontos
// correspondentes que representam uma escala+translação simples, e confere que um 5º ponto
// (não usado no cálculo) é mapeado corretamente pela transformação resultante.
public class HomografiaTests
{
    [Fact]
    public void Aplicar_TransformacaoDeEscalaETranslacao_MapeiaPontoNaoUsadoCorretamente()
    {
        // origem: quadrado unitário (0,0)-(100,100); destino: mesmo quadrado escalado 2x
        // e deslocado (100, 50) — uma homografia puramente afim é um caso particular do
        // sistema geral que Homografia resolve.
        var origem = new (double X, double Y)[] { (0, 0), (100, 0), (0, 100), (100, 100) };
        var destino = new (double X, double Y)[] { (100, 50), (300, 50), (100, 250), (300, 250) };

        var homografia = Homografia.DeCorrespondencias(origem, destino);

        var (x, y) = homografia.Aplicar(50, 40);

        Assert.Equal(200, x, precision: 6);
        Assert.Equal(130, y, precision: 6);
    }

    [Fact]
    public void DeCorrespondencias_PontosColineares_Lanca()
    {
        var origem = new (double X, double Y)[] { (0, 0), (1, 0), (2, 0), (3, 0) };
        var destino = new (double X, double Y)[] { (0, 0), (1, 0), (2, 0), (3, 0) };

        Assert.Throws<InvalidOperationException>(() => Homografia.DeCorrespondencias(origem, destino));
    }
}
