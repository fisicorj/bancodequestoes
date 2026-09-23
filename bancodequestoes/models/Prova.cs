namespace BancoQuestoes.Models;

public class Prova
{
    public int Id { get; set; }
    public required string Titulo { get; set; }

    // Default Disciplina preserva o comportamento de sempre — é o que toda prova
    // pré-existente recebe na migration (backfill).
    public TipoEscopoProva TipoEscopo { get; set; } = TipoEscopoProva.Disciplina;

    // Opcional: nos modos Multidisciplinar/Curso não há Disciplina "dona" (a lista de
    // verdade é ProvaDisciplinas); no modo Disciplina, ProvaService mantém os dois em sincronia.
    public int? DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    // Multi-disciplina de verdade (modos Multidisciplinar/Curso). No modo Disciplina tem
    // exatamente 1 linha (sincronizada com DisciplinaId); nos outros, é a fonte de verdade.
    public List<ProvaDisciplina> ProvaDisciplinas { get; set; } = new();

    // Auditoria: continua apontando pra ESTA matriz mesmo que ela deixe de ser a Ativa
    // depois; nunca reatribuída automaticamente. Restrict: matriz usada não pode ser excluída.
    public int? MatrizReferenciaId { get; set; }
    public MatrizReferencia? MatrizReferencia { get; set; }

    // Opcional: resolve o cabeçalho (via Curso.Instituicao) e o campo "Curso" exportado;
    // nos modos Multidisciplinar/Curso também valida que as Disciplinas pertencem ao Curso.
    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    // Opcional; quando definida, ProvaForm.razor mantém Disciplina/Curso em
    // sincronia com a Turma escolhida.
    public int? TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Metadados opcionais; Ano/Semestre são preenchidos ao escolher uma Turma
    // (ProvaForm.razor) mas continuam editáveis pra provas sem Turma.
    public TipoProva? Tipo { get; set; }
    public int? Ano { get; set; }
    public int? Semestre { get; set; }

    // Mesma ideia de Turma.Bimestre, mas aqui NÃO é obrigatório — Semestre já é
    // metadado solto em Prova, então Bimestre segue a mesma informalidade.
    public int? Bimestre { get; set; }

    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }
    public string? Observacoes { get; set; }

    // Controla se o valor da questão aparece no enunciado impresso: true acrescenta
    // "(1 pt)" ao final; false deixa só no card "Pontuação"/gabarito.
    public bool MostrarValorNoEnunciado { get; set; } = true;

    // Entidade de junção explícita (não List<Questao> direto) porque a relação tem dados
    // próprios (Ordem, Valor) que não pertencem nem à Prova nem à Questao.
    public List<ProvaQuestao> ProvaQuestoes { get; set; } = new();
}

public class ProvaQuestao
{
    public int Id { get; set; }

    public int ProvaId { get; set; }
    public Prova? Prova { get; set; }

    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    public int Ordem { get; set; }

    // "decimal" (não float/double) evita erro de arredondamento binário quando a
    // soma dos valores precisa bater exatamente 10.
    public decimal? Valor { get; set; }
}

// Junção Prova<->Disciplina "com payload" (PercentualPlanejado, distribuição opcional).
// VAZIA no modo Disciplina; nos outros, uma linha por Disciplina participante.
public class ProvaDisciplina
{
    public int Id { get; set; }

    public int ProvaId { get; set; }
    public Prova? Prova { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    // Nulo = sem distribuição configurada; o pool multidisciplinar usa o algoritmo
    // livremente entre as disciplinas (distribuição nunca é obrigatória).
    public int? PercentualPlanejado { get; set; }
}
