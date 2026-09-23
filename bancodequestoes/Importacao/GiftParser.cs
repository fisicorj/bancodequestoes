using System.Globalization;
using System.Text.RegularExpressions;
using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Formato GIFT (Moodle) simplificado — cobre múltipla escolha, Certo/Errado, resposta breve,
// numérica, associação e discursiva (ensaio/redação livre, "{}" vazio — sem resposta esperada,
// preenchida na revisão); "$CATEGORY:"/"$ASSUNTO:" marcam as questões seguintes, resolvido em ImportacaoService.
public static class GiftParser
{
    // Cada questão é "texto { corpo }", opcionalmente com título ::Título:: antes;
    // [\s\S]*? non-greedy garante que o bloco pega só até a PRÓXIMA chave de fechamento.
    private static readonly Regex BlocoRegex = new(
        @"(?:::(?<titulo>[^:]*)::)?(?<texto>[\s\S]*?)\{(?<corpo>[\s\S]*?)\}",
        RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        @"(?<tipo>=|~)\s*(?:%-?\d+(?:\.\d+)?%)?\s*(?<texto>[^~=}]*)",
        RegexOptions.Compiled);

    public static ResultadoImportacao Parse(string texto)
    {
        var resultado = new ResultadoImportacao();

        // Remove comentários (linha inteira começando com //) antes de tudo —
        // nunca vira questão nem afeta categoria.
        var linhasSemComentario = texto.Replace("\r\n", "\n").Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//"));

        // "$CATEGORY: código1, código2" vincula as questões seguintes (até o
        // próximo $CATEGORY:) a Itens de Matriz por Codigo; resolução de fato é do ImportacaoService.
        var segmentos = new List<(List<string> Codigos, (string Disciplina, string Assunto)? DisciplinaAssunto, string Texto)>();
        var codigosAtuais = new List<string>();
        (string Disciplina, string Assunto)? disciplinaAssuntoAtual = null;
        var linhasSegmentoAtual = new List<string>();

        foreach (var linha in linhasSemComentario)
        {
            if (linha.TrimStart().StartsWith("$CATEGORY"))
            {
                segmentos.Add((codigosAtuais, disciplinaAssuntoAtual, string.Join('\n', linhasSegmentoAtual)));
                linhasSegmentoAtual = new List<string>();

                var valor = linha.TrimStart()["$CATEGORY".Length..].TrimStart(':', ' ').Trim();
                codigosAtuais = valor.Length == 0
                    ? new List<string>()
                    : valor.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => c.Length > 0)
                        .ToList();
            }
            else if (linha.TrimStart().StartsWith("$ASSUNTO"))
            {
                segmentos.Add((codigosAtuais, disciplinaAssuntoAtual, string.Join('\n', linhasSegmentoAtual)));
                linhasSegmentoAtual = new List<string>();

                var valor = linha.TrimStart()["$ASSUNTO".Length..].TrimStart(':', ' ').Trim();
                var partes = valor.Split('/', 2);
                disciplinaAssuntoAtual = partes.Length == 2 && partes[0].Trim().Length > 0 && partes[1].Trim().Length > 0
                    ? (partes[0].Trim(), partes[1].Trim())
                    : null;
                if (valor.Length > 0 && disciplinaAssuntoAtual is null)
                {
                    resultado.Erros.Add($"Linha \"$ASSUNTO: {valor}\" mal formatada — use \"Disciplina / Assunto\". As questões seguintes usarão o Assunto escolhido na tela.");
                }
            }
            else
            {
                linhasSegmentoAtual.Add(linha);
            }
        }
        segmentos.Add((codigosAtuais, disciplinaAssuntoAtual, string.Join('\n', linhasSegmentoAtual)));

        foreach (var (codigos, disciplinaAssunto, textoSegmento) in segmentos)
        {
            foreach (Match bloco in BlocoRegex.Matches(textoSegmento))
            {
                var enunciado = bloco.Groups["texto"].Value.Trim();
                var corpo = bloco.Groups["corpo"].Value.Trim();

                if (enunciado.Length == 0)
                {
                    continue; // trecho vazio entre duas questões, ou lixo antes da primeira — ignora silenciosamente
                }

                enunciado = DesescaparGift(enunciado);

                var questao = InterpretarCorpo(enunciado, corpo);
                if (questao is null)
                {
                    resultado.Erros.Add($"Questão \"{Truncar(enunciado)}\": tipo não suportado para importação (ex.: ensaio/redação livre) e foi ignorada.");
                }
                else
                {
                    questao.CodigosItemMatriz = codigos.ToList();
                    questao.DisciplinaSugerida = disciplinaAssunto?.Disciplina;
                    questao.AssuntoSugerido = disciplinaAssunto?.Assunto;
                    resultado.Questoes.Add(questao);
                }
            }
        }

        if (resultado.Questoes.Count == 0 && resultado.Erros.Count == 0)
        {
            resultado.Erros.Add("Nenhuma questão reconhecida no texto — confira se o conteúdo está no formato GIFT.");
        }

        return resultado;
    }

    private static QuestaoImportada? InterpretarCorpo(string enunciado, string corpo)
    {
        if (corpo.Length == 0)
        {
            // {} = ensaio/redação livre no GIFT padrão — o formato não guarda
            // resposta esperada nenhuma pra esse tipo (nem o Moodle guarda),
            // então importa como Discursiva com RespostaEsperada em branco;
            // o professor completa na revisão antes de usar a questão de verdade.
            return new QuestaoImportada
            {
                Enunciado = enunciado,
                Tipo = TipoQuestao.Discursiva,
                Aviso = "Questão discursiva — o GIFT não guarda resposta esperada para esse tipo; preencha depois de importar.",
            };
        }

        // Certo/Errado: {T}, {TRUE}, {F}, {FALSE}
        if (Regex.IsMatch(corpo, @"^(T|TRUE|F|FALSE)$", RegexOptions.IgnoreCase))
        {
            var correta = corpo.Equals("T", StringComparison.OrdinalIgnoreCase) || corpo.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            return new QuestaoImportada
            {
                Enunciado = enunciado,
                Tipo = TipoQuestao.CertoErrado,
                RespostaCertoErrado = correta,
            };
        }

        // Numérica: {#resposta}, {#resposta:tolerancia} ou {#baixo..alto}
        if (corpo.StartsWith('#'))
        {
            var semHash = corpo[1..].Trim();

            var matchFaixa = Regex.Match(semHash, @"^(-?[\d.]+)\s*\.\.\s*(-?[\d.]+)$");
            if (matchFaixa.Success &&
                decimal.TryParse(matchFaixa.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var baixo) &&
                decimal.TryParse(matchFaixa.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var alto))
            {
                return new QuestaoImportada
                {
                    Enunciado = enunciado,
                    Tipo = TipoQuestao.Numerica,
                    NumericaEsperada = (baixo + alto) / 2,
                    NumericaTolerancia = Math.Abs(alto - baixo) / 2,
                };
            }

            // Pode ter respostas alternativas separadas por "=" (ex.: "10:0.5=12:1") —
            // pegamos só a primeira, como fazemos com resposta breve.
            var primeira = semHash.Split('=')[0].Trim();
            var partes = primeira.Split(':');
            if (partes.Length > 0 && decimal.TryParse(partes[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var valorEsperado))
            {
                decimal tolerancia = 0;
                if (partes.Length > 1)
                {
                    decimal.TryParse(partes[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tolerancia);
                }
                return new QuestaoImportada
                {
                    Enunciado = enunciado,
                    Tipo = TipoQuestao.Numerica,
                    NumericaEsperada = valorEsperado,
                    NumericaTolerancia = tolerancia,
                };
            }

            return null;
        }

        var tokens = TokenRegex.Matches(corpo)
            .Select(m => (Tipo: m.Groups["tipo"].Value, Texto: DesescaparGift(m.Groups["texto"].Value.Trim())))
            .Where(t => t.Texto.Length > 0 || t.Tipo.Length > 0)
            .ToList();

        if (tokens.Count == 0)
        {
            return null;
        }

        // Associação: cada alternativa "=termo -> correspondente".
        if (tokens.All(t => t.Tipo == "=") && tokens.All(t => t.Texto.Contains("->")))
        {
            var pares = tokens.Select(t =>
            {
                var partes = t.Texto.Split("->", 2);
                return new ParAssociacaoImportado { Termo = partes[0].Trim(), Correspondente = partes[1].Trim() };
            }).ToList();

            return new QuestaoImportada { Enunciado = enunciado, Tipo = TipoQuestao.Associacao, Pares = pares };
        }

        // Resposta breve: só alternativas "=", sem "->" (uma ou mais respostas
        // aceitas — usamos só a primeira, e avisamos se havia mais de uma).
        if (tokens.All(t => t.Tipo == "="))
        {
            var questaoRb = new QuestaoImportada
            {
                Enunciado = enunciado,
                Tipo = TipoQuestao.RespostaBreve,
                RespostaBreveEsperada = tokens[0].Texto,
            };
            if (tokens.Count > 1)
            {
                questaoRb.Aviso = $"O GIFT aceitava {tokens.Count} respostas alternativas; só a primeira (\"{tokens[0].Texto}\") foi importada.";
            }
            return questaoRb;
        }

        // Caso restante: múltipla escolha (mistura de "=" certa(s) e "~" erradas).
        var alternativas = tokens.Select(t => new AlternativaImportada { Texto = t.Texto, Correta = t.Tipo == "=" }).ToList();
        if (!alternativas.Any(a => a.Correta))
        {
            return null; // sem nenhuma alternativa certa, não dá pra importar
        }

        QuestaoImportada questaoMe = new()
        {
            Enunciado = enunciado,
            Tipo = TipoQuestao.MultiplaEscolha,
            Alternativas = alternativas,
        };
        if (alternativas.Count(a => a.Correta) > 1)
        {
            questaoMe.Aviso = "Havia mais de uma alternativa marcada como correta no GIFT; o sistema só suporta uma — a primeira foi mantida.";
            var jaMarcada = false;
            foreach (var a in questaoMe.Alternativas)
            {
                if (a.Correta && jaMarcada)
                {
                    a.Correta = false;
                }
                else if (a.Correta)
                {
                    jaMarcada = true;
                }
            }
        }
        return questaoMe;
    }

    // GIFT usa \: \= \~ \# \{ \} pra "escapar" esses caracteres quando fazem
    // parte do texto de verdade (não são marcadores de sintaxe).
    private static string DesescaparGift(string texto) => texto
        .Replace(@"\:", ":").Replace(@"\=", "=").Replace(@"\~", "~")
        .Replace(@"\#", "#").Replace(@"\{", "{").Replace(@"\}", "}");

    private static string Truncar(string texto) => texto.Length > 60 ? texto[..60] + "..." : texto;
}
