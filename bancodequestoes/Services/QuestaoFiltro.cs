using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Todos os filtros da tela de listagem de Questões, num objeto só — em vez de
// QuestaoService.ListarAsync receber uns 9 parâmetros soltos. Nullable nos
// enums (em vez das strings "" que a tela usava antes pra representar "sem
// filtro") — QuestaoList.razor liga isso direto num <select> com
// @bind="filtro.Tipo", igual já é feito para Bloom em QuestaoForm.razor.
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
    public HashSet<int> TagIds { get; set; } = new();
    public bool MostrarInativas { get; set; }

    // Alinhamento Curricular / ENADE — CursoId só existe pra escopar os
    // seletores de Matriz/Item na tela (mostrar só as matrizes do curso
    // escolhido, igual Disciplina escopa Assunto); MatrizReferenciaId escopa
    // qual matriz (pra popular o seletor de item); ItemMatrizId é quem de
    // fato filtra as questões (ver QuestaoService.ListarAsync). Itens são
    // flat nesse modelo (sem hierarquia), então o filtro é direto, sem
    // precisar expandir descendentes como no antigo modelo de Diretriz.
    public int CursoId { get; set; }
    public int MatrizReferenciaId { get; set; }
    public int ItemMatrizId { get; set; }
}
