namespace BancoQuestoes.Models;

public class Prova
{
    public int Id { get; set; }
    public required string Titulo { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    // Opcional: o Curso já amarra a Instituicao (Curso.Instituicao), então
    // escolher um Curso aqui resolve de uma vez o cabeçalho (nome, logo,
    // endereço) e o campo "Curso" no documento exportado.
    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    // Opcional (nem toda prova precisa estar amarrada a uma turma específica).
    // Quando definida, o formulário mantém Disciplina/Curso em sincronia com
    // a Turma escolhida — ver ProvaForm.razor.
    public int? TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Bloco de metadados "banco de provas" — tudo opcional, útil pra organizar
    // e (no futuro) gerar estatísticas melhores por tipo/período. Ano/Semestre
    // são preenchidos automaticamente ao escolher uma Turma (ver ProvaForm.razor),
    // mas continuam soltos/editáveis: nem toda prova (ex.: um Simulado avulso)
    // precisa estar amarrada a uma Turma pra ter um período registrado.
    public TipoProva? Tipo { get; set; }
    public int? Ano { get; set; }
    public int? Semestre { get; set; }

    // Mesma ideia do Turma.Bimestre — só faz sentido pra instituições com
    // SistemaPeriodos.SemestralComBimestres. Ao contrário de Turma, aqui NÃO
    // é validado como obrigatório: Semestre já é metadado solto/opcional em
    // Prova (nem toda prova está amarrada a uma Turma/Instituição), então
    // Bimestre segue a mesma informalidade.
    public int? Bimestre { get; set; }

    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }
    public string? Observacoes { get; set; }

    // Essa lista não é List<Questao> direto — é uma lista da entidade de junção
    // ProvaQuestao. Fazemos isso porque a relação "muitos para muitos" entre
    // Prova e Questao tem dados próprios (Ordem, Valor) que não pertencem
    // nem à Prova nem à Questao isoladamente. Esse padrão chama-se
    // "many-to-many com payload" ou simplesmente "entidade de junção explícita".
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

    // "decimal" é o tipo indicado para valores monetários ou de pontuação —
    // ao contrário de float/double, ele não tem erros de arredondamento
    // binário, o que importa quando a soma dos valores precisa bater exatamente 10.
    public decimal? Valor { get; set; }
}
