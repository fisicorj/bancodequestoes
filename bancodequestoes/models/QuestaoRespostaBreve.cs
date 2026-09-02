namespace BancoQuestoes.Models;

// Resposta curta: uma palavra ou frase pequena, com uma única linha em branco
// na prova impressa (diferente de "Discursiva", que reserva várias linhas).
public class QuestaoRespostaBreve : Questao
{
    public required string RespostaEsperada { get; set; }
}
