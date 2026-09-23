using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Parâmetros de um sorteio, já achatados a partir do GeradorInput, pra
// GeradorProvaService.Sortear ficar uma função pura (sem acesso a banco).
public sealed class SorteioParametros
{
    // Blueprint: cada assunto vem com o % da Quantidade total (chave =
    // AssuntoId). Sempre obrigatório ter ao menos um e a soma bater 100.
    public required Dictionary<int, int> PercPorAssunto { get; init; }

    public required List<TipoQuestao> TiposPermitidos { get; init; }

    // Opcional: vazio = não filtrar por Bloom. Com algo, só questões já
    // classificadas num dos níveis marcados entram no sorteio.
    public required Dictionary<NivelBloom, int> PercPorBloom { get; init; }

    public required int Quantidade { get; init; }
    public required int PercFacil { get; init; }
    public required int PercMedia { get; init; }
    public required int PercDificil { get; init; }
    public required HashSet<int> IdsJaSelecionados { get; init; }

    // Quantas vezes cada questão já foi usada — só pra desempatar a favor de
    // quem foi menos exposto, nunca pra excluir do sorteio.
    public required Dictionary<int, int> UsosPorQuestao { get; init; }
}

public sealed class SorteioResultado
{
    public required List<Questao> Sorteadas { get; init; }
    public string? Aviso { get; init; }

    // true quando validação básica impediu qualquer sorteio — a tela usa pra pular a distribuição de valor.
    public bool Abortado { get; init; }
}

// Resultado de SortearPorBlueprintCurricular — como SorteioResultado, mas com Planejado×Obtido por item.
public sealed class ResultadoBlueprintCurricular
{
    public required List<Questao> Sorteadas { get; init; }
    public string? Aviso { get; init; }
    public bool Abortado { get; init; }

    // ItemMatrizReferenciaId -> quantidade planejada/obtida; sem entrada conta como 0.
    public required Dictionary<int, int> QtdPlanejadaPorItem { get; init; }
    public required Dictionary<int, int> QtdObtidaPorItem { get; init; }
}
