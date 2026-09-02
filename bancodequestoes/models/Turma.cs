using System.ComponentModel.DataAnnotations.Schema;

namespace BancoQuestoes.Models;

// Turma = uma oferta específica de uma Disciplina dentro de um Curso, num
// período (ano/semestre) — ex.: "EC3A" de Arquitetura de Computadores no
// curso de Engenharia da Computação, 2026/2. Junto com Curso.Instituicao,
// completa a hierarquia Instituição > Curso > Disciplina > Turma.
//
// CursoId e DisciplinaId são obrigatórios (nunca nulos) de propósito: uma
// Turma sem Curso ou sem Disciplina não faz sentido nessa modelagem — por
// isso as duas FKs são Restrict no ApplicationDbContext (apagar um Curso ou
// Disciplina com turmas vinculadas falha, em vez de apagar a turma junto ou
// deixá-la "solta").
//
// Existe pra preparar o sistema pra funcionalidades futuras (alunos vinculados
// à turma, resultados de prova por turma, análise de desempenho) — por
// enquanto só serve pra Prova.TurmaId (opcional) marcar "essa prova é dessa
// turma", que já ajuda a organizar/filtrar as provas.
public class Turma
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public int Ano { get; set; }
    public int Semestre { get; set; } = 1;

    // Só usado (e só obrigatório — ver CursoService.ValidarTurmaAsync) quando
    // a Instituição do Curso usa SistemaPeriodos.SemestralComBimestres: qual
    // dos dois bimestres DENTRO desse Semestre (1º ou 2º), não um número de
    // 1 a 4 pro ano inteiro. Continua null pra instituições puramente
    // semestrais.
    public int? Bimestre { get; set; }

    public int CursoId { get; set; }
    public Curso? Curso { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // "EC3A — 2026/2" — não é uma coluna no banco, só uma forma conveniente
    // de exibir o período junto do nome sem repetir essa formatação em cada
    // tela (lista de turmas, seletor na prova, cabeçalho da exportação).
    [NotMapped]
    public string Rotulo => Bimestre is { } bim
        ? $"{Nome} — {Ano}/{Semestre} ({bim}º bim)"
        : $"{Nome} — {Ano}/{Semestre}";
}
