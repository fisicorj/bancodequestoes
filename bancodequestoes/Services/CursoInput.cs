using System.ComponentModel.DataAnnotations;

namespace BancoQuestoes.Services;

// Movido de CursoForm.razor (classe privada) — mesmo padrão dos outros Inputs.
public sealed class CursoInput
{
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public int InstituicaoId { get; set; }

    // Área de Curso nacional que este curso representa; null = sem área
    // escolhida ainda, e matrizes ENADE/DCN não aparecem pra ele até então.
    public int? AreaCursoId { get; set; }
}

// Movido de TurmaForm.razor (classe privada).
public sealed class TurmaInput
{
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public int CursoId { get; set; }
    public int DisciplinaId { get; set; }

    [Range(2000, 2100, ErrorMessage = "Informe um ano válido.")]
    public int Ano { get; set; }

    public int Semestre { get; set; } = 1;

    // Só obrigatório quando o Curso pertence a uma Instituição com SistemaPeriodos.SemestralComBimestres.
    public int? Bimestre { get; set; }
}

// DTO do vínculo CursoDisciplina — pra criar/editar manualmente
// (Semestre/CargaHoraria) sem depender só do upsert automático via Turma.
public sealed class CursoDisciplinaInput
{
    public int CursoId { get; set; }
    public int DisciplinaId { get; set; }
    public int? Semestre { get; set; }
    public int? CargaHoraria { get; set; }
    public bool Ativa { get; set; } = true;
}
