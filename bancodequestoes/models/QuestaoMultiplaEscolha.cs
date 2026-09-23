namespace BancoQuestoes.Models;

public class QuestaoMultiplaEscolha : Questao
{
    // Alternativas ficam em tabela separada (relação um-pra-muitos) em vez de
    // colunas fixas "AlternativaA/B/...", pra suportar qualquer quantidade.
    public char RespostaCorreta { get; set; }

    public List<AlternativaQuestao> Alternativas { get; set; } = new();
}

public class AlternativaQuestao
{
    public int Id { get; set; }

    public int QuestaoMultiplaEscolhaId { get; set; }
    public QuestaoMultiplaEscolha? Questao { get; set; }

    public char Letra { get; set; }
    public required string Texto { get; set; }
}
