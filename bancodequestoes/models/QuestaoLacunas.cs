namespace BancoQuestoes.Models;

// "Cloze" simplificado: o professor escreve o Enunciado (herdado de Questao)
// marcando cada lacuna com "___" (três ou mais underscores), na ordem em que
// aparecem no texto. Cada item de Lacunas guarda a resposta esperada de uma
// delas, casando pela posição (a primeira lacuna do texto usa Lacunas[0], e
// assim por diante). Diferente do Cloze completo do Moodle, aqui não dá pra
// misturar múltipla escolha/numérica dentro da lacuna — só texto esperado.
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
