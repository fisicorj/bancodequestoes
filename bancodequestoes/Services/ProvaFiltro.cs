namespace BancoQuestoes.Services;

// Filtro de listagem de Provas — mesmo padrão do QuestaoFiltro.
public sealed class ProvaFiltro
{
    public string? Texto { get; set; }
    public int DisciplinaId { get; set; }
}
