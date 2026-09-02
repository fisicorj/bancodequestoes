namespace BancoQuestoes.Services;

// Filtro da tela de histórico/auditoria (HistoricoQuestoes.razor) — mesmo
// padrão de QuestaoFiltro, só que mais enxuto: aqui o que importa é achar
// rápido "o que mudou numa questão/assunto/disciplina", não replicar todos
// os filtros da listagem de questões.
public sealed class HistoricoFiltro
{
    public string? Texto { get; set; }
    public int DisciplinaId { get; set; }
    public int AssuntoId { get; set; }
}
