namespace BancoQuestoes.Models;

public class Assunto
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    public List<Questao> Questoes { get; set; } = new();
}
