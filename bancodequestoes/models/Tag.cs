namespace BancoQuestoes.Models;

// Vocabulário compartilhado entre professores (igual Disciplina/Assunto); Nome único
// (índice no ApplicationDbContext) evita duplicatas tipo "Pipeline"/"pipeline".
public class Tag
{
    public int Id { get; set; }
    public required string Nome { get; set; }
}
