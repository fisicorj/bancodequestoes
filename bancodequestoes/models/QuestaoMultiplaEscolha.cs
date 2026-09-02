namespace BancoQuestoes.Models;

// ": Questao" aqui é herança: QuestaoMultiplaEscolha "é uma" Questao,
// e ganha automaticamente todas as propriedades de Questao (Id, Enunciado, etc.)
// além das que ela mesma declara.
public class QuestaoMultiplaEscolha : Questao
{
    // Guardamos qual letra é a correta aqui. As alternativas em si (texto de cada
    // uma) ficam em uma tabela separada, porque é uma relação "um para muitos"
    // (uma questão pode ter 4, 5 ou 6 alternativas) — colocar "AlternativaA",
    // "AlternativaB" como colunas fixas não escalaria bem.
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
