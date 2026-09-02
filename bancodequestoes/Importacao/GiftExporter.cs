using System.Text;
using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Exportação simétrica ao GiftParser: cobre os mesmos 5 tipos que ele
// consegue importar (múltipla escolha, Certo/Errado, resposta breve,
// numérica, associação). Discursiva e Lacunas não têm um equivalente fiel em
// GIFT simples, então entram em "Ignoradas" em vez de virar algo quebrado.
public static class GiftExporter
{
    public static (string Conteudo, List<string> Ignoradas) Gerar(List<Questao> questoes)
    {
        var sb = new StringBuilder();
        var ignoradas = new List<string>();

        foreach (var q in questoes)
        {
            switch (q)
            {
                case QuestaoMultiplaEscolha me when me.Alternativas.Count >= 2:
                    sb.AppendLine(Escapar(q.Enunciado) + " {");
                    foreach (var a in me.Alternativas.OrderBy(a => a.Letra))
                    {
                        var marcador = a.Letra == me.RespostaCorreta ? "=" : "~";
                        sb.AppendLine($"\t{marcador}{Escapar(a.Texto)}");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine();
                    break;

                case QuestaoCertoErrado ce:
                    sb.AppendLine($"{Escapar(q.Enunciado)} {{{(ce.RespostaCorreta ? "T" : "F")}}}");
                    sb.AppendLine();
                    break;

                case QuestaoRespostaBreve rb:
                    sb.AppendLine($"{Escapar(q.Enunciado)} {{={Escapar(rb.RespostaEsperada)}}}");
                    sb.AppendLine();
                    break;

                case QuestaoNumerica num:
                    var faixa = num.Tolerancia > 0
                        ? $"{num.RespostaEsperada.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{num.Tolerancia.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                        : num.RespostaEsperada.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    sb.AppendLine($"{Escapar(q.Enunciado)} {{#{faixa}}}");
                    sb.AppendLine();
                    break;

                case QuestaoAssociacao assoc when assoc.Pares.Count >= 2:
                    sb.AppendLine(Escapar(q.Enunciado) + " {");
                    foreach (var p in assoc.Pares.OrderBy(p => p.Ordem))
                    {
                        sb.AppendLine($"\t={Escapar(p.Termo)} -> {Escapar(p.Correspondente)}");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine();
                    break;

                default:
                    ignoradas.Add(Truncar(q.Enunciado));
                    break;
            }
        }

        return (sb.ToString(), ignoradas);
    }

    // GIFT usa : = ~ # { } como caracteres de sintaxe — escapa com "\" quando
    // fazem parte do texto de verdade. A barra invertida precisa ser escapada
    // PRIMEIRO, senão as barras inseridas pelos replaces seguintes seriam
    // escapadas de novo por engano.
    private static string Escapar(string texto) => texto
        .Replace(@"\", @"\\")
        .Replace(":", @"\:").Replace("=", @"\=").Replace("~", @"\~")
        .Replace("#", @"\#").Replace("{", @"\{").Replace("}", @"\}")
        .Replace("\r", " ").Replace("\n", " ");

    private static string Truncar(string texto) => texto.Length > 60 ? texto[..60] + "..." : texto;
}
