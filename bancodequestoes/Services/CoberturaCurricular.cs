namespace BancoQuestoes.Services;

using BancoQuestoes.Models;

// Resultado do dashboard "Cobertura Curricular" (item 19 do pedido) — sempre
// escoado a UMA MatrizReferencia específica (ex.: "ENADE 2023"), nunca ao
// curso como um todo, porque cada matriz tem seu próprio conjunto de itens.
// Isso é diferente do antigo CoberturaCurricularResultado (recurso anterior,
// removido), que era por Curso — agora o curso pode ter várias matrizes
// (versionamento), então a tela sempre pede "qual matriz" primeiro.
public sealed class CoberturaCurricularResultado
{
    public required MatrizReferencia Matriz { get; init; }

    // "Cobertura institucional" (item 14 do pedido): todas as questões
    // ATIVAS do curso — só é exibida pra quem já tem acesso a essa matriz
    // (mesma instituição do curso, ou Admin — ver MatrizReferenciaService.
    // GarantirAcessoAoCursoAsync), então já não é mais um vazamento pra fora
    // da instituição como acontecia antes desta rodada.
    public required int TotalQuestoesDoCurso { get; init; }

    // "Cobertura disponível para minhas provas" (item 14): quantas dessas
    // questões o usuário atual realmente PODERIA usar num gerador de prova
    // (mesma regra de QuestaoVisibilidade.VisivelPara já usada lá) — sempre
    // menor ou igual a TotalQuestoesDoCurso. Documentado como o recorte
    // "pessoal" ao lado do institucional; o detalhamento por item
    // (Grupos/Linhas) continua no nível institucional — dividir cada linha
    // também em institucional×pessoal ficaria sem função de perfil suficiente
    // pra justificar hoje (ver relatório final).
    public required int TotalQuestoesMinhasProvas { get; init; }

    // Uma "linha" (grupo) por TipoItemMatriz presente na matriz, na ordem em
    // que o enum declara (Perfil, Competência, Conteúdo...) — QuestaoForm.razor
    // e a página de cobertura reusam essa mesma agrupação/ordem.
    public required List<GrupoCobertura> Grupos { get; init; }

    // Cross-tab Item x Disciplina (item 20 do pedido, "se viável sem muito
    // impacto") — fica vazio quando não há dados a mostrar; a tela decide se
    // renderiza a seção ou não com base em CrossTabDisciplinas.Count == 0.
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

    // Limiar arbitrário (mesmo espírito do farol de qualidade) só pra
    // destacar na tela itens com pouquíssima cobertura (⚠, item 19/28).
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
