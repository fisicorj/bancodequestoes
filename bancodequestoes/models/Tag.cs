namespace BancoQuestoes.Models;

// Vocabulário de tags compartilhado entre todos os professores (igual
// Disciplina/Assunto) — uma tag como "pipeline" é a mesma tag pra todo mundo,
// em vez de cada professor ter sua própria lista isolada. Isso é o que
// permite reaproveitar/autocompletar tags já usadas por outros. Nome único
// (índice configurado no ApplicationDbContext) pra não acumular duplicatas
// tipo "Pipeline" e "pipeline" como tags diferentes.
public class Tag
{
    public int Id { get; set; }
    public required string Nome { get; set; }
}
