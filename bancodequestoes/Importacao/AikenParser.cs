using System.Text.RegularExpressions;
using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Formato Aiken: só múltipla escolha, texto puro (enunciado, opções "A) texto",
// linha "ANSWER: B"); linha em branco separa questões, mas ANSWER: já fecha a atual.
public static class AikenParser
{
    private static readonly Regex OpcaoRegex = new(@"^\s*([A-Za-z])[).]\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RespostaRegex = new(@"^\s*ANSWER\s*[:.]?\s*([A-Za-z])\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ResultadoImportacao Parse(string texto)
    {
        var resultado = new ResultadoImportacao();
        var linhas = texto.Replace("\r\n", "\n").Split('\n');

        string? enunciadoAtual = null;
        var opcoesAtuais = new List<string>();
        var numeroLinhaEnunciado = 0;

        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i];
            var linhaAparada = linha.Trim();

            if (linhaAparada.Length == 0)
            {
                continue;
            }

            var matchOpcao = OpcaoRegex.Match(linha);
            var matchResposta = RespostaRegex.Match(linha);

            if (matchResposta.Success && enunciadoAtual is not null && opcoesAtuais.Count > 0)
            {
                var letraResposta = char.ToUpperInvariant(matchResposta.Groups[1].Value[0]);
                var indiceCorreta = letraResposta - 'A';

                if (indiceCorreta < 0 || indiceCorreta >= opcoesAtuais.Count)
                {
                    resultado.Erros.Add($"Linha {i + 1}: resposta \"{letraResposta}\" não corresponde a nenhuma alternativa da questão \"{Truncar(enunciadoAtual)}\".");
                }
                else
                {
                    resultado.Questoes.Add(new QuestaoImportada
                    {
                        Enunciado = enunciadoAtual,
                        Tipo = TipoQuestao.MultiplaEscolha,
                        Alternativas = opcoesAtuais
                            .Select((texto, idx) => new AlternativaImportada { Texto = texto, Correta = idx == indiceCorreta })
                            .ToList(),
                    });
                }

                enunciadoAtual = null;
                opcoesAtuais = new List<string>();
            }
            else if (matchOpcao.Success && enunciadoAtual is not null)
            {
                opcoesAtuais.Add(matchOpcao.Groups[2].Value.Trim());
            }
            else if (!matchOpcao.Success)
            {
                // Não é opção nem ANSWER — começo de nova questão; se havia uma em
                // andamento sem ANSWER, é descartada com aviso.
                if (enunciadoAtual is not null)
                {
                    resultado.Erros.Add($"Linha {numeroLinhaEnunciado + 1}: questão \"{Truncar(enunciadoAtual)}\" não teve uma linha ANSWER: e foi ignorada.");
                }
                enunciadoAtual = linhaAparada;
                numeroLinhaEnunciado = i;
                opcoesAtuais = new List<string>();
            }
        }

        if (enunciadoAtual is not null)
        {
            resultado.Erros.Add($"Linha {numeroLinhaEnunciado + 1}: questão \"{Truncar(enunciadoAtual)}\" não teve uma linha ANSWER: e foi ignorada.");
        }

        return resultado;
    }

    private static string Truncar(string texto) => texto.Length > 60 ? texto[..60] + "..." : texto;
}
