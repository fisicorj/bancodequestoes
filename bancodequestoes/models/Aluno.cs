namespace BancoQuestoes.Models;

// Aluno é cadastro próprio (não é ApplicationUser/Identity) — acesso à prova online é por
// Código de acesso da AplicacaoProva, sem login/senha de aluno.
public class Aluno
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public string? Email { get; set; }
    public string? Matricula { get; set; }

    // Opcional, mesmo padrão de ApplicationUser.InstituicaoId — SetNull ao excluir a instituição.
    public int? InstituicaoId { get; set; }
    public Instituicao? Instituicao { get; set; }

    // Desativar em vez de excluir preserva TurmaAluno/RespostaProvaOnline já vinculadas.
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<TurmaAluno> TurmaAlunos { get; set; } = new();
}
