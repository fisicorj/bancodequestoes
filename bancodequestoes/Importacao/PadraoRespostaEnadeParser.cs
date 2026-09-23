using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace BancoQuestoes.Importacao;

// Parser do "Padrão de Resposta" das discursivas (D1/D2), documento separado da
// prova/gabarito; lê um TEXTO oficial por questão e nunca completa conteúdo ausente.
public static class PadraoRespostaEnadeParser
{
    // Mesmo formato de cabeçalho de EnadeProvaParser.QuestaoBoundaryRegex, duplicado de
    // propósito (documento diferente); nunca validado contra um Padrão de Resposta real.
    private static readonly Regex QuestaoDiscursivaHeaderRegex = new(
        @"QUEST(?:Ã|A)O\s+DISCURSIVA\s+(?<num>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Subitem "a)", "a.", "a-", "a:" no início da linha — minúsculo de propósito,
    // pois um padrão de resposta discursiva nunca tem alternativas A-E maiúsculas.
    private static readonly Regex SubitemRegex = new(
        @"^\(?(?<item>[a-e])\)?[\.\)\-:]\s*(?<resto>.+)$", RegexOptions.Compiled);

    // "(valor: 5,0 pontos)": só EXTRAÍDO pra preencher Pontuacao de forma
    // estruturada, nunca removido do texto oficial.
    private static readonly Regex PontuacaoRegex = new(
        @"valor:?\s*(?<valor>\d+(?:[,\.]\d+)?)\s*pontos?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string MarcadorPagina = "[[PAGINA:";

    // Um padrão de resposta oficial encontrado pra uma questão discursiva.
    public sealed class PadraoRespostaDiscursivaImportado
    {
        // Texto OFICIAL completo, nunca resumido/reescrito — vira
        // QuestaoImportacaoEnade.RespostaEsperadaDiscursiva.
        public string TextoCompleto { get; set; } = "";

        // Subitens com pontuação, só quando a separação foi segura (ver
        // MontarPadrao); lista vazia é o caso comum, não um erro.
        public List<ItemDiscursivoImportacaoEnade> Itens { get; set; } = new();

        public int? PaginaOrigem { get; set; }
    }

    public sealed class ResultadoPadraoResposta
    {
        // Chave "D1"/"D2" (mesmo formato de
        // QuestaoImportacaoEnade.NumeroOriginal pra discursivas).
        public Dictionary<string, PadraoRespostaDiscursivaImportado> Respostas { get; } = new();
        public List<string> Alertas { get; } = new();
    }

    public static ResultadoPadraoResposta Extrair(byte[] pdfBytes)
    {
        var resultado = new ResultadoPadraoResposta();

        PdfDocument documento;
        try
        {
            documento = PdfDocument.Open(pdfBytes);
        }
        catch (Exception ex)
        {
            resultado.Alertas.Add($"Não foi possível ler o PDF do padrão de resposta discursiva ({ex.GetType().Name}) — o padrão foi ignorado, preencha manualmente na revisão.");
            return resultado;
        }

        using (documento)
        {
            if (documento.NumberOfPages == 0)
            {
                resultado.Alertas.Add("O PDF do padrão de resposta discursiva não tem nenhuma página.");
                return resultado;
            }

            // Mesmo truque de marcador de página embutido de EnadeProvaParser.Extrair
            // — recupera PaginaOrigem via busca retroativa (PaginaAntesDe).
            var textoConcatenado = new StringBuilder();
            foreach (var pagina in documento.GetPages())
            {
                string texto;
                try
                {
                    texto = EnadeProvaParser.TextoEmOrdemDeLeitura(pagina);
                }
                catch
                {
                    continue;
                }

                textoConcatenado.Append(MarcadorPagina).Append(pagina.Number).Append("]]\n").Append(texto).Append("\n\n");
            }

            var textoCompleto = textoConcatenado.ToString();
            var matches = QuestaoDiscursivaHeaderRegex.Matches(textoCompleto);

            if (matches.Count == 0)
            {
                resultado.Alertas.Add("Não foi possível localizar nenhuma \"QUESTÃO DISCURSIVA\" no PDF do padrão de resposta — confira o formato do arquivo ou preencha manualmente na revisão.");
                return resultado;
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var fim = i + 1 < matches.Count ? matches[i + 1].Index : textoCompleto.Length;
                var corpo = textoCompleto[(match.Index + match.Length)..fim];

                var numero = $"D{int.Parse(match.Groups["num"].Value)}";
                var paginaOrigem = PaginaAntesDe(textoCompleto, match.Index);
                var padrao = MontarPadrao(corpo, paginaOrigem);

                if (padrao.TextoCompleto.Length == 0)
                {
                    resultado.Alertas.Add($"A seção \"{numero}\" foi encontrada no padrão de resposta, mas está vazia — confira o arquivo ou preencha manualmente na revisão.");
                    continue;
                }

                if (resultado.Respostas.ContainsKey(numero))
                {
                    resultado.Alertas.Add($"A questão \"{numero}\" aparece mais de uma vez no PDF do padrão de resposta — usada a primeira ocorrência, confira manualmente.");
                    continue;
                }

                resultado.Respostas[numero] = padrao;
            }
        }

        if (resultado.Respostas.Count == 0 && resultado.Alertas.Count == 0)
        {
            resultado.Alertas.Add("Nenhum padrão de resposta discursiva foi reconhecido neste arquivo — confira o formato ou preencha manualmente na revisão.");
        }

        return resultado;
    }

    private static int? PaginaAntesDe(string texto, int posicao)
    {
        var ultimoIndice = texto.LastIndexOf(MarcadorPagina, Math.Min(posicao, texto.Length) - 1, StringComparison.Ordinal);
        if (ultimoIndice < 0)
        {
            return null;
        }

        var inicioNumero = ultimoIndice + MarcadorPagina.Length;
        var fimNumero = texto.IndexOf(']', inicioNumero);
        if (fimNumero < 0)
        {
            return null;
        }

        return int.TryParse(texto[inicioNumero..fimNumero], out var numero) ? numero : null;
    }

    private static PadraoRespostaDiscursivaImportado MontarPadrao(string corpo, int? paginaOrigem)
    {
        var linhas = corpo.Replace("\r\n", "\n").Split('\n')
            .Where(l => !l.TrimStart().StartsWith(MarcadorPagina, StringComparison.Ordinal))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var padrao = new PadraoRespostaDiscursivaImportado { PaginaOrigem = paginaOrigem };
        var textoLinhas = new List<string>();
        ItemDiscursivoImportacaoEnade? atual = null;

        foreach (var linha in linhas)
        {
            var m = SubitemRegex.Match(linha);
            if (m.Success)
            {
                var resto = m.Groups["resto"].Value.Trim();
                atual = new ItemDiscursivoImportacaoEnade
                {
                    Codigo = m.Groups["item"].Value.ToLowerInvariant(),
                    RespostaPadrao = resto,
                };
                padrao.Itens.Add(atual);
            }
            else if (atual is not null)
            {
                // Linha física de continuação (quebra pela largura da página):
                // junta com espaço; Pontuacao só é checada depois, no texto final.
                atual.RespostaPadrao = string.IsNullOrEmpty(atual.RespostaPadrao) ? linha : $"{atual.RespostaPadrao} {linha}";
            }

            // TextoCompleto: linha física quebrada pela largura vira espaço, não
            // parágrafo novo; só considera quebra de verdade quando a linha inicia um subitem.
            if (textoLinhas.Count == 0 || m.Success)
            {
                textoLinhas.Add(linha);
            }
            else
            {
                textoLinhas[^1] = $"{textoLinhas[^1]} {linha}";
            }
        }

        // Pontuação: procurada no texto FINAL de cada subitem, depois de
        // todas as linhas de continuação já dobradas nele, nunca só na primeira linha física.
        foreach (var item in padrao.Itens)
        {
            var mValor = PontuacaoRegex.Match(item.RespostaPadrao);
            if (mValor.Success && decimal.TryParse(mValor.Groups["valor"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
            {
                item.Pontuacao = valor;
            }
        }

        padrao.TextoCompleto = string.Join("\n", textoLinhas).Trim();

        // Só mantém a estrutura de subitens com 2+ itens e nenhum vazio; um único
        // "subitem" é provável falso positivo — descarta e mantém só o texto completo.
        if (padrao.Itens.Count < 2 || padrao.Itens.Any(it => string.IsNullOrWhiteSpace(it.RespostaPadrao)))
        {
            padrao.Itens.Clear();
        }

        return padrao;
    }
}
