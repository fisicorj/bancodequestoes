using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes do motor de geração, puros, sem banco: SortearPorDisciplina e
// AlocarPorItemMaisRaroPrimeiro (questão com várias competências, uma vez só).
public class GeradorProvaServiceTests
{
    private static QuestaoMultiplaEscolha NovaQuestao(int id, int assuntoId, int disciplinaId, Dificuldade dificuldade = Dificuldade.Media) => new()
    {
        Id = id,
        Enunciado = $"Questão {id}",
        AssuntoId = assuntoId,
        Assunto = new Assunto { Id = assuntoId, Nome = $"Assunto {assuntoId}", DisciplinaId = disciplinaId },
        Dificuldade = dificuldade,
        TipoQuestao = TipoQuestao.MultiplaEscolha,
        Visibilidade = VisibilidadeQuestao.Compartilhada,
        Ativa = true,
        RespostaCorreta = 'A',
    };

    private static List<Questao> CriarQuestoes(int assuntoId, int disciplinaId, int quantidade, int idBase)
    {
        var lista = new List<Questao>();
        for (var i = 0; i < quantidade; i++)
        {
            lista.Add(NovaQuestao(idBase + i, assuntoId, disciplinaId));
        }
        return lista;
    }

    // 20 questões, 50/30/20% entre 3 disciplinas, disponibilidade exata
    // (10/6/4) — resultado deve bater exato, sem aviso de falta.
    [Fact]
    public void SortearPorDisciplina_ExemploDoPedido_20Questoes_50_30_20_Retorna_10_6_4()
    {
        var servico = new GeradorProvaService();

        var disciplinas = new List<Disciplina>
        {
            new() { Id = 1, Nome = "Redes" },
            new() { Id = 2, Nome = "Sistemas Operacionais" },
            new() { Id = 3, Nome = "Segurança da Informação" },
        };

        var questoes = new List<Questao>();
        questoes.AddRange(CriarQuestoes(assuntoId: 10, disciplinaId: 1, quantidade: 10, idBase: 1000));
        questoes.AddRange(CriarQuestoes(assuntoId: 20, disciplinaId: 2, quantidade: 6, idBase: 2000));
        questoes.AddRange(CriarQuestoes(assuntoId: 30, disciplinaId: 3, quantidade: 4, idBase: 3000));

        var resultado = servico.SortearPorDisciplina(
            questoesDisponiveis: questoes,
            disciplinasDisponiveis: disciplinas,
            percPorDisciplina: new Dictionary<int, int> { [1] = 50, [2] = 30, [3] = 20 },
            tiposPermitidos: new List<TipoQuestao> { TipoQuestao.MultiplaEscolha },
            quantidade: 20,
            percFacil: 0,
            percMedia: 100,
            percDificil: 0,
            idsJaSelecionados: new HashSet<int>(),
            usosPorQuestao: new Dictionary<int, int>(),
            idsAEvitar: new HashSet<int>());

        Assert.False(resultado.Abortado);
        Assert.Null(resultado.Aviso);
        Assert.Equal(20, resultado.Sorteadas.Count);
        Assert.Equal(10, resultado.Sorteadas.Count(q => q.Assunto!.DisciplinaId == 1));
        Assert.Equal(6, resultado.Sorteadas.Count(q => q.Assunto!.DisciplinaId == 2));
        Assert.Equal(4, resultado.Sorteadas.Count(q => q.Assunto!.DisciplinaId == 3));

        // Confirma que não há QuestaoId duplicado na prova gerada.
        Assert.Equal(resultado.Sorteadas.Count, resultado.Sorteadas.Select(q => q.Id).Distinct().Count());
    }

    // Disponibilidade insuficiente não aborta nem lança — a geração acontece
    // com o que deu, e o Aviso relata quanto faltou.
    [Fact]
    public void SortearPorDisciplina_DisponibilidadeInsuficiente_NuncaFalhaSilenciosamente()
    {
        var servico = new GeradorProvaService();

        var disciplinas = new List<Disciplina> { new() { Id = 1, Nome = "Redes" } };

        // Só 3 questões disponíveis, mas o plano pede 10 (100% de 10).
        var questoes = CriarQuestoes(assuntoId: 10, disciplinaId: 1, quantidade: 3, idBase: 1000);

        var resultado = servico.SortearPorDisciplina(
            questoesDisponiveis: questoes,
            disciplinasDisponiveis: disciplinas,
            percPorDisciplina: new Dictionary<int, int> { [1] = 100 },
            tiposPermitidos: new List<TipoQuestao> { TipoQuestao.MultiplaEscolha },
            quantidade: 10,
            percFacil: 0,
            percMedia: 100,
            percDificil: 0,
            idsJaSelecionados: new HashSet<int>(),
            usosPorQuestao: new Dictionary<int, int>(),
            idsAEvitar: new HashSet<int>());

        Assert.False(resultado.Abortado);
        Assert.Equal(3, resultado.Sorteadas.Count);
        Assert.NotNull(resultado.Aviso);
        Assert.Contains("Redes", resultado.Aviso);
        Assert.Contains("faltaram 7", resultado.Aviso);
    }

    // Validação de FORMA (percentuais que não somam 100%) — abortado, com
    // aviso explícito, nunca uma exceção não tratada.
    [Fact]
    public void SortearPorDisciplina_PercentuaisNaoSomam100_Aborta()
    {
        var servico = new GeradorProvaService();

        var resultado = servico.SortearPorDisciplina(
            questoesDisponiveis: new List<Questao>(),
            disciplinasDisponiveis: new List<Disciplina>(),
            percPorDisciplina: new Dictionary<int, int> { [1] = 50, [2] = 40 },
            tiposPermitidos: new List<TipoQuestao> { TipoQuestao.MultiplaEscolha },
            quantidade: 10,
            percFacil: 30,
            percMedia: 50,
            percDificil: 20,
            idsJaSelecionados: new HashSet<int>(),
            usosPorQuestao: new Dictionary<int, int>(),
            idsAEvitar: new HashSet<int>());

        Assert.True(resultado.Abortado);
        Assert.NotNull(resultado.Aviso);
        Assert.Empty(resultado.Sorteadas);
    }

    // Uma questão com múltiplas competências ocupa no máximo uma posição na
    // alocação, mesmo candidata a mais de um item do blueprint.
    [Fact]
    public void AlocarPorItemMaisRaroPrimeiro_QuestaoComMultiplosItens_NuncaDuplicaNaSelecao()
    {
        var itemC07 = new ItemMatrizReferencia { Id = 1, Codigo = "C07", Titulo = "Competência 7", MatrizReferenciaId = 1 };
        var itemC08 = new ItemMatrizReferencia { Id = 2, Codigo = "C08", Titulo = "Competência 8", MatrizReferenciaId = 1 };

        // Q1 é a única candidata de C08 (mais raro) e também atende C07; "mais
        // raro primeiro" aloca Q1 em C08, sobrando só Q2 pra C07.
        var q1 = new QuestaoMultiplaEscolha
        {
            Id = 1,
            Enunciado = "Q1",
            AssuntoId = 1,
            RespostaCorreta = 'A',
            ItensMatriz = new List<ItemMatrizReferencia> { itemC07, itemC08 },
        };
        var q2 = new QuestaoMultiplaEscolha
        {
            Id = 2,
            Enunciado = "Q2",
            AssuntoId = 1,
            RespostaCorreta = 'A',
            ItensMatriz = new List<ItemMatrizReferencia> { itemC07 },
        };

        var pool = new List<Questao> { q1, q2 };
        var qtdPlanejadaPorItem = new Dictionary<int, int> { [itemC07.Id] = 1, [itemC08.Id] = 1 };

        var (selecionadas, obtidoPorItem, faltandoPorItem) = GeradorProvaService.AlocarPorItemMaisRaroPrimeiro(
            pool,
            qtdPlanejadaPorItem,
            new HashSet<int>(),
            ordenarExcluindoJaAlocados: true,
            escolher: (elegiveis, qtd) => elegiveis.Take(qtd).ToList());

        Assert.Equal(2, selecionadas.Count);
        Assert.Equal(selecionadas.Count, selecionadas.Select(q => q.Id).Distinct().Count());
        Assert.Equal(1, obtidoPorItem.GetValueOrDefault(itemC07.Id));
        Assert.Equal(1, obtidoPorItem.GetValueOrDefault(itemC08.Id));
        Assert.Empty(faltandoPorItem);
    }

    // Mesma ideia, mas com disponibilidade insuficiente pra um item — o
    // diagnóstico (FaltandoPorItem) precisa relatar isso, nunca lançar.
    [Fact]
    public void AlocarPorItemMaisRaroPrimeiro_DisponibilidadeInsuficiente_RelataNoFaltandoPorItem()
    {
        var item = new ItemMatrizReferencia { Id = 1, Codigo = "C09", Titulo = "Competência 9", MatrizReferenciaId = 1 };
        var q1 = new QuestaoMultiplaEscolha
        {
            Id = 1,
            Enunciado = "Q1",
            AssuntoId = 1,
            RespostaCorreta = 'A',
            ItensMatriz = new List<ItemMatrizReferencia> { item },
        };

        var (selecionadas, obtidoPorItem, faltandoPorItem) = GeradorProvaService.AlocarPorItemMaisRaroPrimeiro(
            new List<Questao> { q1 },
            new Dictionary<int, int> { [item.Id] = 2 },
            new HashSet<int>(),
            ordenarExcluindoJaAlocados: true,
            escolher: (elegiveis, qtd) => elegiveis.Take(qtd).ToList());

        Assert.Single(selecionadas);
        Assert.Equal(1, obtidoPorItem.GetValueOrDefault(item.Id));
        Assert.Equal(1, faltandoPorItem.GetValueOrDefault(item.Id));
    }
}
