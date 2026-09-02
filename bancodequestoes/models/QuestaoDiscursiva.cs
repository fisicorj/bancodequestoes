namespace BancoQuestoes.Models;

public class QuestaoDiscursiva : Questao
{
    public required string RespostaEsperada { get; set; }

    // Rubrica opcional de correção — útil na hora de você corrigir provas,
    // não é obrigatório preencher.
    public string? CriterioAvaliacao { get; set; }
}
