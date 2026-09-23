using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// "Farol" de qualidade: completude dos metadados (Bloom, referência,
// explicação...) — não é auditoria de conteúdo.
public static class QualidadeQuestao
{
    public static QualidadeResultado Calcular(Questao questao)
    {
        var itens = new List<CriterioQualidade>
        {
            new("Nível de Bloom classificado", questao.Bloom is not null, 20),
            new("Referência/fonte preenchida", !string.IsNullOrWhiteSpace(questao.Referencia), 15),
            new("Explicação da resposta preenchida", !string.IsNullOrWhiteSpace(questao.Explicacao), 25),
            new("Pelo menos uma tag", questao.Tags.Count > 0, 10),
            new(
                "Imagens com texto alternativo",
                questao.Imagens.Count == 0 || questao.Imagens.All(i => !string.IsNullOrWhiteSpace(i.TextoAlternativo)),
                10),
            CriterioPorTipo(questao),
        };

        var pesoTotal = itens.Sum(i => i.Peso);
        var pesoAtendido = itens.Where(i => i.Atendido).Sum(i => i.Peso);
        var percentual = pesoTotal == 0 ? 0 : (int)Math.Round(100.0 * pesoAtendido / pesoTotal);

        return new QualidadeResultado { Percentual = percentual, Itens = itens };
    }

    // Critério específico por tipo: um sinal de capricho não garantido pela
    // validação de cadastro. Certo/Errado e Resposta breve não têm um extra óbvio.
    private static CriterioQualidade CriterioPorTipo(Questao questao) => questao switch
    {
        QuestaoMultiplaEscolha me => new(
            "4+ alternativas, sem duplicadas",
            me.Alternativas.Count >= 4 &&
                me.Alternativas.Select(a => a.Texto.Trim().ToLowerInvariant()).Distinct().Count() == me.Alternativas.Count,
            20),
        QuestaoAssociacao assoc => new("3+ pares de associação", assoc.Pares.Count >= 3, 20),
        QuestaoLacunas lac => new("2+ lacunas no texto", lac.Lacunas.Count >= 2, 20),
        QuestaoDiscursiva d => new("Critério de avaliação preenchido", !string.IsNullOrWhiteSpace(d.CriterioAvaliacao), 20),
        QuestaoNumerica num => new("Tolerância definida (± maior que zero)", num.Tolerancia > 0, 20),
        _ => new("Estrutura da resposta completa", true, 20),
    };
}

public sealed record CriterioQualidade(string Rotulo, bool Atendido, int Peso);

public sealed class QualidadeResultado
{
    public required int Percentual { get; init; }
    public required List<CriterioQualidade> Itens { get; init; }

    public List<string> CriteriosFaltando => Itens.Where(i => !i.Atendido).Select(i => i.Rotulo).ToList();

    // Cor do "farol" pra UI — centralizado aqui pra não repetir os limiares em
    // cada tela que mostra a badge.
    public string CorFarol => Percentual switch
    {
        >= 80 => "success",
        >= 50 => "warning",
        _ => "danger",
    };
}
