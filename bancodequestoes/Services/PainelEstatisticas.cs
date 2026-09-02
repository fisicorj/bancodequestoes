namespace BancoQuestoes.Services;

// Resultado completo do painel de Estatisticas.razor — a tela só cuida de
// desenhar as barras de progresso (escala/percentual), os números e
// agrupamentos já vêm prontos do EstatisticaService.
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

// Uma linha do painel de cobertura (Estatisticas.razor) — quantas questões
// ATIVAS e VISÍVEIS pro professor cada Assunto tem, e como elas se
// distribuem entre os níveis de Bloom. Inclui assuntos com Total = 0 de
// propósito: o objetivo desse painel é achar justamente os assuntos com
// pouca ou nenhuma questão, não só listar os que já têm.
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
