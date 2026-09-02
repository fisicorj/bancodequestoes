namespace BancoQuestoes.Models;

public class Assunto
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    // Toda FK no EF Core costuma vir em par: o Id (int) para a coluna real no banco,
    // e a propriedade de navegação (Disciplina) para você acessar o objeto completo
    // sem escrever SQL. O EF Core casa os dois automaticamente pelo nome "DisciplinaId".
    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    public List<Questao> Questoes { get; set; } = new();
}
