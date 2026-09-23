using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// "O que o professor escolheu" sobre o escopo da prova. Diferente de
// EscopoQuestoes abaixo ("o que perguntar ao banco") — ParaEscopoQuestoes traduz entre os dois.
public sealed class EscopoProvaConfig
{
    public TipoEscopoProva Tipo { get; set; } = TipoEscopoProva.Disciplina;

    // Obrigatório no modo Curso. Nos modos Disciplina/Multidisciplinar é só informativo.
    public int? CursoId { get; set; }

    // Modo Disciplina: exatamente 1 id. Multidisciplinar: 2+. Curso: sempre vazio.
    public HashSet<int> DisciplinaIds { get; set; } = new();

    // Só no modo Curso — quando definida, o pool só aceita questões com
    // pelo menos um Item desta matriz. Nula = curso "livre".
    public int? MatrizReferenciaId { get; set; }

    // Único tradutor ProvaInput -> EscopoProvaConfig, usado por
    // ProvaService e ProvaForm.razor pra nunca duplicar essa conversão.
    public static EscopoProvaConfig DoProvaInput(ProvaInput modelo) => new()
    {
        Tipo = modelo.TipoEscopo,
        CursoId = modelo.CursoId == 0 ? null : modelo.CursoId,
        DisciplinaIds = modelo.TipoEscopo switch
        {
            TipoEscopoProva.Disciplina => modelo.DisciplinaId == 0 ? new HashSet<int>() : new HashSet<int> { modelo.DisciplinaId },
            TipoEscopoProva.Multidisciplinar => new HashSet<int>(modelo.DisciplinaIds),
            _ => new HashSet<int>(),
        },
        MatrizReferenciaId = modelo.MatrizReferenciaId,
    };

    // Único tradutor "Tipo + campos" -> "o que perguntar ao banco", usado
    // por GeradorProvaService e ProvaService.
    public EscopoQuestoes ParaEscopoQuestoes() => new()
    {
        CursoId = Tipo == TipoEscopoProva.Curso ? CursoId : null,
        DisciplinaIds = Tipo == TipoEscopoProva.Curso ? new HashSet<int>() : new HashSet<int>(DisciplinaIds),
        MatrizReferenciaId = MatrizReferenciaId,
    };

    // Validação de FORMA só — se os ids existem/pertencem a quem pode usar é responsabilidade de ProvaService.
    public string? Validar() => Tipo switch
    {
        TipoEscopoProva.Disciplina when DisciplinaIds.Count == 0 =>
            "Selecione uma disciplina.",
        TipoEscopoProva.Multidisciplinar when DisciplinaIds.Count < 2 =>
            "Selecione ao menos 2 disciplinas (com 1 só, use o escopo \"Uma disciplina\").",
        TipoEscopoProva.Curso when CursoId is null or 0 =>
            "Selecione um curso.",
        _ => null,
    };
}

// O que QuestaoQueryService.ObterCandidatasAsync de fato usa, sem ambiguidade
// de "modo" — reutilizável fora da geração de prova também.
public sealed class EscopoQuestoes
{
    public int? CursoId { get; set; }
    public HashSet<int> DisciplinaIds { get; set; } = new();
    public int? MatrizReferenciaId { get; set; }
    public HashSet<int> ItemMatrizIds { get; set; } = new();
}
