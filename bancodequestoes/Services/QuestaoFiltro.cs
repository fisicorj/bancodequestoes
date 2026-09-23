using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Todos os filtros da tela de listagem de Questões, num objeto só (em vez de
// vários parâmetros soltos). Enums nullable representam "sem filtro".
public sealed class QuestaoFiltro
{
    public string? Texto { get; set; }
    public int DisciplinaId { get; set; }
    public int AssuntoId { get; set; }
    public TipoQuestao? Tipo { get; set; }
    public Dificuldade? Dificuldade { get; set; }
    public VisibilidadeQuestao? Visibilidade { get; set; }
    public NivelBloom? Bloom { get; set; }
    public OrigemQuestao? Origem { get; set; }

    // Suporte ENADE: Ano reaproveita o campo já existente; SecaoEnade só faz
    // sentido com Origem=Enade, mas o filtro não impõe isso.
    public int? Ano { get; set; }
    public SecaoEnade? SecaoEnade { get; set; }

    public HashSet<int> TagIds { get; set; } = new();
    public bool MostrarInativas { get; set; }

    // CursoId só escopa os seletores de Matriz/Item na tela; quem de fato
    // filtra é ItemMatrizId. AreaCursoId filtra por aplicabilidade acadêmica.
    public int CursoId { get; set; }
    public int AreaCursoId { get; set; }
    public int MatrizReferenciaId { get; set; }
    public int ItemMatrizId { get; set; }
}
