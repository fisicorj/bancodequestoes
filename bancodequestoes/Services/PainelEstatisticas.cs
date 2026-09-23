namespace BancoQuestoes.Services;

// Resultado completo do painel de Estatisticas.razor, já pronto do EstatisticaService.
public sealed class PainelEstatisticas
{
    public required int TotalQuestoesAtivas { get; init; }
    public required int TotalDisciplinas { get; init; }
    public required int TotalAssuntos { get; init; }
    public required int MinhasProvasTotal { get; init; }
    public required int ProvasEsteMes { get; init; }

    public required List<ItemContagem> PorDisciplina { get; init; }
    public required List<ItemContagem> PorDificuldade { get; init; }
    public required List<ItemContagem> PorTipo { get; init; }
    public required List<ItemContagem> ProvasPorMes { get; init; }
}

public sealed class ItemContagem
{
    public required string Rotulo { get; init; }
    public int Quantidade { get; init; }
}

// Uma linha do painel de cobertura: questões ativas/visíveis por Assunto e
// nível de Bloom. Inclui Total = 0 de propósito, pra achar assuntos carentes.
public sealed class CoberturaAssunto
{
    public required string Disciplina { get; init; }
    public required string Assunto { get; init; }
    public required int Total { get; init; }
    public required int Lembrar { get; init; }
    public required int Entender { get; init; }
    public required int Aplicar { get; init; }
    public required int Analisar { get; init; }
    public required int Avaliar { get; init; }
    public required int Criar { get; init; }
    public required int SemClassificacao { get; init; }
}
