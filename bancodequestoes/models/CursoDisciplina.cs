namespace BancoQuestoes.Models;

// Vínculo explícito Curso<->Disciplina, independente de já existir Turma dela; Disciplina
// continua GLOBAL/reutilizável, índice único (CursoId, DisciplinaId) impede repetição.
public class CursoDisciplina
{
    public int Id { get; set; }

    public int CursoId { get; set; }
    public Curso? Curso { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    // Opcionais — o vínculo em si já vale sem esses detalhes preenchidos.
    public int? Semestre { get; set; }
    public int? CargaHoraria { get; set; }

    // Desativar em vez de excluir preserva Turmas já referenciadas (mesmo padrão de
    // ItemMatrizReferencia.Ativo/Questao.Ativa).
    public bool Ativa { get; set; } = true;
}
