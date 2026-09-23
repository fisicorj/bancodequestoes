using System.ComponentModel.DataAnnotations;

namespace BancoQuestoes.Services;

public sealed class AlunoInput
{
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public string? Email { get; set; }
    public string? Matricula { get; set; }

    // Opcional, mesmo padrão de Curso/Instituição — sem instituição, o aluno
    // ainda pode ser matriculado normalmente em qualquer Turma.
    public int? InstituicaoId { get; set; }
}
