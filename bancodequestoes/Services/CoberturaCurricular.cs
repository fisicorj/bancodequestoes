namespace BancoQuestoes.Services;

using BancoQuestoes.Models;


public sealed class CoberturaCurricularResultado
{
    public required MatrizReferencia Matriz { get; init; }

    
    public required int TotalQuestoesDoCurso { get; init; }

    
    public required int TotalQuestoesMinhasProvas { get; init; }

    
    public required List<GrupoCobertura> Grupos { get; init; }

    
    public required List<CelulaCrossTabDisciplina> CrossTabDisciplinas { get; init; }
}

public sealed class GrupoCobertura
{
    public required TipoItemMatriz Tipo { get; init; }
    public required List<LinhaCoberturaItem> Linhas { get; init; }

    public int TotalItens => Linhas.Count;
    public int ItensComQuestao => Linhas.Count(l => l.Quantidade > 0);
    public int PercentualCobertura => TotalItens == 0 ? 0 : (int)Math.Round(100.0 * ItensComQuestao / TotalItens);
}

public sealed class LinhaCoberturaItem
{
    public required ItemMatrizReferencia Item { get; init; }
    public required int Quantidade { get; init; }

    public bool CoberturaBaixa => Quantidade is > 0 and < 3;
    public bool SemCobertura => Quantidade == 0;
}

public sealed class CelulaCrossTabDisciplina
{
    public required int ItemMatrizReferenciaId { get; init; }
    public required int DisciplinaId { get; init; }
    public required string DisciplinaNome { get; init; }
    public required int Quantidade { get; init; }
}
