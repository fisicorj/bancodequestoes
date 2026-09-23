using System.ComponentModel.DataAnnotations.Schema;

namespace BancoQuestoes.Models;

// Turma = oferta de uma Disciplina dentro de um Curso, num período (ano/semestre).
// CursoId/DisciplinaId são obrigatórios e Restrict (apagar um com turmas vinculadas falha).
public class Turma
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public int Ano { get; set; }
    public int Semestre { get; set; } = 1;

    // Só usado/obrigatório (ver CursoService.ValidarTurmaAsync) quando a Instituição usa
    // SemestralComBimestres: qual bimestre DENTRO do Semestre (1º/2º), não 1-4 no ano.
    public int? Bimestre { get; set; }

    public int CursoId { get; set; }
    public Curso? Curso { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // "EC3A — 2026/2": não é coluna no banco, evita repetir a formatação em cada tela.
    [NotMapped]
    public string Rotulo => Bimestre is { } bim
        ? $"{Nome} — {Ano}/{Semestre} ({bim}º bim)"
        : $"{Nome} — {Ano}/{Semestre}";
}
