namespace BancoQuestoes.Models;

// Como Resposta Breve, mas a resposta é um número com margem de erro aceita
// (ex.: "9.8 ± 0.2"), útil pra correção de contas com arredondamento.
public class QuestaoNumerica : Questao
{
    public decimal RespostaEsperada { get; set; }
    public decimal Tolerancia { get; set; }
}
