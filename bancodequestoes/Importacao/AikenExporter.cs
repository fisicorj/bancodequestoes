using System.Text;
using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Exportação simétrica ao AikenParser: só múltipla escolha tem equivalente
// no formato Aiken, então qualquer outro tipo é reportado em "Ignoradas"
// em vez de gerar uma linha quebrada no arquivo.
public static class AikenExporter
{
    public static (string Conteudo, List<string> Ignoradas) Gerar(List<Questao> questoes)
    {
        var sb = new StringBuilder();
        var ignoradas = new List<string>();

        foreach (var q in questoes)
        {
            if (q is QuestaoMultiplaEscolha me && me.Alternativas.Count >= 2)
            {
                sb.AppendLine(LimparLinha(q.Enunciado));
                foreach (var a in me.Alternativas.OrderBy(a => a.Letra))
                {
                    sb.AppendLine($"{a.Letra}) {LimparLinha(a.Texto)}");
                }
                sb.AppendLine($"ANSWER: {me.RespostaCorreta}");
                sb.AppendLine();
            }
            else
            {
                ignoradas.Add(Truncar(q.Enunciado));
            }
        }

        return (sb.ToString(), ignoradas);
    }

    // Aiken é linha-a-linha — uma quebra de linha dentro do enunciado ou de uma
    // alternativa quebraria o formato, então achata tudo numa linha só.
    private static string LimparLinha(string texto) => texto.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Truncar(string texto) => texto.Length > 60 ? texto[..60] + "..." : texto;
}
