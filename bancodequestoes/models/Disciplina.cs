namespace BancoQuestoes.Models;

public class Disciplina
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    // Identificador estável e opcional pra reconhecer "a" Disciplina Formação Geral sem
    // comparar Nome; Codigo é só pra comparação programática, nullable e sem índice único.
    public string? Codigo { get; set; }

    // Único valor de Codigo usado até agora — QuestaoService/DbSeeder/testes sempre
    // referenciam esta constante, nunca a string crua.
    public const string CodigoFormacaoGeral = "FORMACAO_GERAL";

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public List<Assunto> Assuntos { get; set; } = new();
}
