namespace BancoQuestoes.Models;

// "Cloze" simplificado: lacunas marcadas com "___" no Enunciado casam por posição com
// Lacunas[0], [1]... — diferente do Moodle, só aceita texto esperado (sem múltipla escolha/numérica).
public class QuestaoLacunas : Questao
{
    public List<LacunaResposta> Lacunas { get; set; } = new();
}

public class LacunaResposta
{
    public int Id { get; set; }

    public int QuestaoLacunasId { get; set; }
    public QuestaoLacunas? Questao { get; set; }

    public int Ordem { get; set; }
    public required string RespostaEsperada { get; set; }
}
