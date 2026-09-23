namespace BancoQuestoes.Models;

// Vínculo Turma<->Aluno (matrícula), mesmo padrão de CursoDisciplina — índice único
// (TurmaId, AlunoId) impede duplicidade; Ativa permite desmatricular sem apagar histórico.
public class TurmaAluno
{
    public int Id { get; set; }

    public int TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public int AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
