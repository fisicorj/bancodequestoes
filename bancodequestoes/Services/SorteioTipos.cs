using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Parâmetros de um sorteio — já "achatados" a partir do GeradorInput +
// seleção de assuntos/tipos da tela, pra ProvaService.Sortear ficar uma
// função pura (sem acesso a banco).
public sealed class SorteioParametros
{
    // Blueprint de avaliação: cada assunto marcado no gerador vem com o % da
    // Quantidade total que deve vir dele (chave = AssuntoId). Sempre
    // obrigatório ter pelo menos um e a soma bater 100 — não existe "sem
    // filtrar por assunto".
    public required Dictionary<int, int> PercPorAssunto { get; init; }

    public required List<TipoQuestao> TiposPermitidos { get; init; }

    // Opcional: vazio significa "não filtrar por Bloom" (aceita qualquer
    // questão, inclusive sem classificação). Quando tem algo, cada nível
    // marcado vem com o % que deve vir dele (mesma regra do
    // PercPorAssunto) — só questões JÁ classificadas num desses níveis
    // entram no sorteio nesse caso.
    public required Dictionary<NivelBloom, int> PercPorBloom { get; init; }

    public required int Quantidade { get; init; }
    public required int PercFacil { get; init; }
    public required int PercMedia { get; init; }
    public required int PercDificil { get; init; }
    public required HashSet<int> IdsJaSelecionados { get; init; }

    // Quantas vezes cada questão já foi usada em provas — usado só pra
    // desempatar a favor de quem foi menos exposto (ver
    // ProvaService.SortearAleatorios), nunca pra excluir uma questão do
    // sorteio. Questão sem entrada aqui conta como 0 usos.
    public required Dictionary<int, int> UsosPorQuestao { get; init; }
}

public sealed class SorteioResultado
{
    public required List<Questao> Sorteadas { get; init; }
    public string? Aviso { get; init; }

    // true quando uma validação básica (tipo/assunto/quantidade/percentual)
    // impediu qualquer sorteio — a tela usa isso pra saber se deve pular a
    // distribuição de valor também (igual o "return" antecipado do código original).
    public bool Abortado { get; init; }
}

// Resultado de ProvaService.SortearPorBlueprintCurricular — mesmo espírito
// de SorteioResultado acima, mas guardando também Planejado×Obtido POR ITEM
// (item 6 da 2ª rodada de revisão: "não misturar alocação no blueprint com
// cobertura real da questão" — aqui é especificamente a ALOCAÇÃO: quantas
// questões o sorteio de fato reservou pra cada posição do blueprint daquele
// item, não quantas questões selecionadas TOCAM aquele item no total —
// ProvaForm.CalcularCoberturaCurricular() continua sendo o cálculo de
// cobertura real, ao vivo, sobre "selecionadas").
public sealed class ResultadoBlueprintCurricular
{
    public required List<Questao> Sorteadas { get; init; }
    public string? Aviso { get; init; }
    public bool Abortado { get; init; }

    // ItemMatrizReferenciaId -> quantidade planejada/obtida. Só tem entrada
    // pros itens com percentual > 0; item sem entrada em QtdObtidaPorItem
    // conta como 0.
    public required Dictionary<int, int> QtdPlanejadaPorItem { get; init; }
    public required Dictionary<int, int> QtdObtidaPorItem { get; init; }
}
