namespace BancoQuestoes.Services;

// Filtro da tela de histórico/auditoria (HistoricoQuestoes.razor).
public sealed class HistoricoFiltro
{
    public string? Texto { get; set; }
    public int DisciplinaId { get; set; }
    public int AssuntoId { get; set; }
}
