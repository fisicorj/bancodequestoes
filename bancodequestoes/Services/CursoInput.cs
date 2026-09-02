using System.ComponentModel.DataAnnotations;

namespace BancoQuestoes.Services;

// Movido de CursoForm.razor (classe privada) — mesmo padrão dos outros Inputs.
public sealed class CursoInput
{
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public int InstituicaoId { get; set; }

    // Opcional (item da 2ª rodada de revisão) — qual Área de Curso nacional
    // este curso representa (ver AreaCurso). 0/null = sem área escolhida
    // ainda; matrizes ENADE/DCN não aparecem pra esse curso até o professor
    // escolher uma.
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

    // Só obrigatório (ver CursoService.ValidarTurmaAsync) quando o Curso
    // escolhido pertence a uma Instituição com
    // SistemaPeriodos.SemestralComBimestres.
    public int? Bimestre { get; set; }
}
