using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BancoQuestoes.Importacao;

// Parser do arquivo de gabarito oficial (opcional), puro, sem banco. Tenta duas
// estratégias: GEOMÉTRICA (pareia rótulo/letra por altura numa tabela) e LINHA ÚNICA (regex).
public static class GabaritoEnadeParser
{
    // Aceita "01 A", "01 - A", "01) A", "Questão 01: A", "D1 A" — usada só
    // pela estratégia de linha única (fallback, ver comentário da classe).
    private static readonly Regex LinhaGabaritoRegex = new(
        @"(?:QUEST(?:Ã|A)O\s+)?(?<num>D?\d{1,2})\s*[-:\).]?\s*(?<letra>[A-E])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Rótulo de questão numa célula de "Item" da tabela: "QUESTÃO 07",
    // "QUESTÃO DISCURSIVA 1" (com ou sem acento, maiúsc/minúsc).
    private static readonly Regex RotuloQuestaoRegex = new(
        @"^QUEST(?:Ã|A)O\s+(?:DISCURSIVA\s+(?<disc>\d{1,2})|(?<num>\d{1,2}))\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Célula de "Gabarito" com uma letra só, ou marcador de anulada/sem
    // gabarito ("***", comum em tabelas oficiais do INEP).
    private static readonly Regex RespostaCelulaRegex = new(
        @"^(?<letra>[A-E])$|^(?<anulada>\*{2,3})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed class ResultadoGabarito
    {
        // Chave normalizada igual QuestaoImportacaoEnade.NumeroOriginal
        // ("01".."40", "D1"/"D2") -> letra do gabarito (maiúscula).
        public Dictionary<string, char> Respostas { get; } = new();
        public List<string> Alertas { get; } = new();
    }

    // "logar" (opcional, mesmo espírito do hook de EnadeProvaParser.Extrair):
    // diagnóstico ativo em ambas as estratégias pra qualquer formato futuro ainda não previsto.
    private const int MaxLinhasLogadas = 500;

    public static ResultadoGabarito Extrair(byte[] pdfBytes, Action<string>? logar = null)
    {
        var resultado = new ResultadoGabarito();

        PdfDocument documento;
        try
        {
            documento = PdfDocument.Open(pdfBytes);
        }
        catch (Exception ex)
        {
            resultado.Alertas.Add($"Não foi possível ler o PDF do gabarito oficial ({ex.GetType().Name}) — o gabarito foi ignorado, preencha manualmente na revisão.");
            logar?.Invoke($"[Gabarito] Falha ao abrir o PDF: {ex.GetType().Name}: {ex.Message}");
            return resultado;
        }

        using (documento)
        {
            var totalPaginas = documento.NumberOfPages;
            logar?.Invoke($"[Gabarito] PDF aberto com {totalPaginas} página(s).");

            foreach (var pagina in documento.GetPages())
            {
                var colunas = EnadeProvaParser.DividirPaginaEmColunas(pagina);
                if (colunas is not null)
                {
                    logar?.Invoke($"[Gabarito] Página {pagina.Number}: duas colunas detectadas ({colunas.Value.Esquerda.Count} linha(s) à esquerda, {colunas.Value.Direita.Count} à direita) — tentando parear como tabela Item/Gabarito.");
                    var casouAlgo = ExtrairViaColunas(colunas.Value.Esquerda, colunas.Value.Direita, pagina.Number, resultado, logar);
                    if (casouAlgo)
                    {
                        continue;
                    }
                    logar?.Invoke($"[Gabarito] Página {pagina.Number}: pareamento por coluna não encontrou nenhum rótulo de questão reconhecível — caindo para o parsing por linha única nesta página.");
                }

                ExtrairViaRegexDeLinha(pagina, resultado, logar);
            }
        }

        logar?.Invoke($"[Gabarito] Resumo final: {resultado.Respostas.Count} resposta(s) única(s) reconhecida(s) (chaves: {string.Join(", ", resultado.Respostas.Keys.OrderBy(k => k))}).");

        if (resultado.Respostas.Count == 0)
        {
            resultado.Alertas.Add("Nenhuma entrada de gabarito foi reconhecida neste arquivo — confira o formato ou preencha o gabarito manualmente na revisão.");
        }

        return resultado;
    }

    // Estratégia 1: pareia cada linha da coluna "Item" com a mais próxima da coluna
    // "Gabarito", filtrando cabeçalhos antes e só dentro de um limiar de distância.
    private static bool ExtrairViaColunas(
        List<(string Linha, double Y)> esquerda,
        List<(string Linha, double Y)> direita,
        int numeroPagina,
        ResultadoGabarito resultado,
        Action<string>? logar)
    {
        var rotulos = new List<(string Chave, bool Discursiva, double Y, string LinhaOriginal)>();
        foreach (var (linha, y) in esquerda)
        {
            var m = RotuloQuestaoRegex.Match(linha.Trim());
            if (!m.Success)
            {
                continue;
            }

            if (m.Groups["disc"].Success)
            {
                rotulos.Add(($"D{int.Parse(m.Groups["disc"].Value)}", true, y, linha));
            }
            else
            {
                rotulos.Add((m.Groups["num"].Value.PadLeft(2, '0'), false, y, linha));
            }
        }

        if (rotulos.Count == 0)
        {
            return false;
        }

        var respostas = new List<(char? Letra, bool Anulada, double Y, string LinhaOriginal)>();
        foreach (var (linha, y) in direita)
        {
            var m = RespostaCelulaRegex.Match(linha.Trim());
            if (!m.Success)
            {
                continue;
            }

            if (m.Groups["anulada"].Success)
            {
                respostas.Add((null, true, y, linha));
            }
            else
            {
                respostas.Add((char.ToUpperInvariant(m.Groups["letra"].Value[0]), false, y, linha));
            }
        }

        logar?.Invoke($"[Gabarito] Página {numeroPagina}: {rotulos.Count} rótulo(s) de questão e {respostas.Count} célula(s) de resposta reconhecidos nas colunas.");

        // Espaçamento vertical típico entre rótulos consecutivos, como referência
        // de "mesma linha da tabela"; com menos de 2 rótulos, usa tolerância fixa.
        double alturaTipicaDeLinha;
        if (rotulos.Count >= 2)
        {
            var gaps = new List<double>();
            for (var i = 1; i < rotulos.Count; i++)
            {
                gaps.Add(Math.Abs(rotulos[i].Y - rotulos[i - 1].Y));
            }
            gaps.Sort();
            alturaTipicaDeLinha = gaps[gaps.Count / 2]; // mediana
            if (alturaTipicaDeLinha <= 0)
            {
                alturaTipicaDeLinha = 12.0;
            }
        }
        else
        {
            alturaTipicaDeLinha = 12.0;
        }

        var toleranciaY = alturaTipicaDeLinha * 0.6;
        var casouAlgo = false;

        foreach (var rotulo in rotulos)
        {
            if (respostas.Count == 0)
            {
                break;
            }

            var maisProxima = respostas
                .Select((r, idx) => (r, idx, dist: Math.Abs(r.Y - rotulo.Y)))
                .OrderBy(x => x.dist)
                .First();

            if (maisProxima.dist > toleranciaY)
            {
                logar?.Invoke($"[Gabarito] Página {numeroPagina}: rótulo \"{Truncar(rotulo.LinhaOriginal, 60)}\" (questão {rotulo.Chave}) sem célula de resposta na mesma altura (mais próxima a {maisProxima.dist:F1}pt, tolerância {toleranciaY:F1}pt) — nenhuma resposta aplicada a essa questão.");
                continue;
            }

            var resposta = maisProxima.r;
            casouAlgo = true;

            if (rotulo.Discursiva)
            {
                // Discursiva não usa letra de múltipla escolha — só loga, não é
                // erro (ImportadorProvaEnadeService só aplica gabarito a MultiplaEscolha).
                logar?.Invoke($"[Gabarito] Página {numeroPagina}: questão discursiva {rotulo.Chave} pareada com \"{Truncar(resposta.LinhaOriginal, 20)}\" (ignorado, discursiva não usa letra).");
                continue;
            }

            if (resposta.Anulada)
            {
                resultado.Alertas.Add($"Questão \"{rotulo.Chave}\" consta como anulada (\"{resposta.LinhaOriginal}\") no gabarito oficial — nenhuma resposta correta a aplicar.");
                logar?.Invoke($"[Gabarito] Página {numeroPagina}: questão {rotulo.Chave} pareada com marcador de anulada (\"{resposta.LinhaOriginal}\").");
                continue;
            }

            var letra = resposta.Letra!.Value;
            logar?.Invoke($"[Gabarito] Página {numeroPagina}: questão {rotulo.Chave} = {letra} (rótulo a {rotulo.Y:F1}pt, resposta a {resposta.Y:F1}pt, distância {maisProxima.dist:F1}pt).");

            if (resultado.Respostas.TryGetValue(rotulo.Chave, out var existente))
            {
                if (existente != letra)
                {
                    resultado.Alertas.Add($"Gabarito ambíguo para a questão \"{rotulo.Chave}\": encontrado \"{existente}\" e \"{letra}\" em páginas/linhas diferentes — nenhum dos dois foi aplicado, preencha manualmente.");
                    resultado.Respostas.Remove(rotulo.Chave);
                }
                continue;
            }

            resultado.Respostas[rotulo.Chave] = letra;
        }

        return casouAlgo;
    }

    // Estratégia 2 (fallback): página de coluna única, número e letra na
    // mesma linha de texto extraído ("01 A", "Questão 01: A"...).
    private static void ExtrairViaRegexDeLinha(Page pagina, ResultadoGabarito resultado, Action<string>? logar)
    {
        string texto;
        try
        {
            texto = EnadeProvaParser.TextoEmOrdemDeLeitura(pagina);
        }
        catch (Exception ex)
        {
            logar?.Invoke($"[Gabarito] Página {pagina.Number}: TextoEmOrdemDeLeitura lançou exceção ({ex.GetType().Name}) — página pulada inteira.");
            return;
        }

        var linhasDaPagina = texto.Replace("\r\n", "\n").Split('\n');
        logar?.Invoke($"[Gabarito] Página {pagina.Number}: {linhasDaPagina.Length} linha(s) de texto extraídas (parsing por linha única).");

        var totalLinhasLogadas = 0;

        foreach (var linhaCrua in linhasDaPagina)
        {
            var linha = linhaCrua.Trim();
            if (linha.Length == 0)
            {
                continue;
            }

            var matches = LinhaGabaritoRegex.Matches(linha);

            if (totalLinhasLogadas < MaxLinhasLogadas)
            {
                totalLinhasLogadas++;
                var resumoMatches = matches.Count == 0
                    ? "SEM MATCH"
                    : string.Join(", ", matches.Select(m => $"{m.Groups["num"].Value}={m.Groups["letra"].Value}"));
                logar?.Invoke($"[Gabarito] Página {pagina.Number}: linha \"{Truncar(linha, 100)}\" -> {resumoMatches}");
            }

            foreach (Match m in matches)
            {
                var numeroCru = m.Groups["num"].Value.ToUpperInvariant();
                var numero = numeroCru.StartsWith("D", StringComparison.Ordinal)
                    ? numeroCru
                    : numeroCru.PadLeft(2, '0');
                var letra = char.ToUpperInvariant(m.Groups["letra"].Value[0]);

                // Mesma questão com letras DIFERENTES é leitura ambígua — marca
                // alerta e não guarda nenhuma; repetição com a MESMA letra é ignorada.
                if (resultado.Respostas.TryGetValue(numero, out var existente))
                {
                    if (existente != letra)
                    {
                        resultado.Alertas.Add($"Gabarito ambíguo para a questão \"{numero}\": encontrado \"{existente}\" e \"{letra}\" em linhas diferentes — nenhum dos dois foi aplicado, preencha manualmente.");
                        resultado.Respostas.Remove(numero);
                    }
                    continue;
                }

                resultado.Respostas[numero] = letra;
            }
        }
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max] + "…";
}
