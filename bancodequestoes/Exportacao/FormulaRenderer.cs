using System.Text.RegularExpressions;

namespace BancoQuestoes.Exportacao;

// Fórmulas em LaTeX (entre $...$ ou $$...$$) escritas no enunciado precisam
// virar imagem para aparecer certinho no DOCX/PDF — nenhum dos dois exportadores
// tem um motor de composição matemática embutido. Em vez de implementar um,
// usamos um serviço público de renderização (CodeCogs), que devolve um PNG
// pronto pra cada fórmula. Isso exige internet no momento da exportação; se a
// chamada falhar (sem rede, serviço fora do ar), mantemos o LaTeX bruto como
// texto em vez de perder o conteúdo da questão.
public static class FormulaRenderer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private static readonly Regex FormulaRegex = new(@"\$\$(.+?)\$\$|\$(?!\$)(.+?)\$", RegexOptions.Singleline | RegexOptions.Compiled);

    // Divide um texto em trechos alternados de texto puro e imagem renderizada
    // de fórmula, na ordem em que aparecem — permite que os exportadores montem
    // um único parágrafo misturando texto normal com as fórmulas inline.
    public static async Task<List<TrechoTexto>> DividirEmTrechosAsync(string texto)
    {
        var partes = new List<TrechoTexto>();

        if (string.IsNullOrEmpty(texto) || !texto.Contains('$'))
        {
            return partes;
        }

        var matches = FormulaRegex.Matches(texto);
        if (matches.Count == 0)
        {
            return partes;
        }

        var pos = 0;
        foreach (Match m in matches)
        {
            if (m.Index > pos)
            {
                partes.Add(new TrechoTexto { Texto = texto[pos..m.Index] });
            }

            var latex = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            var imagem = await RenderizarAsync(latex);
            partes.Add(imagem is not null
                ? new TrechoTexto { ImagemPng = imagem }
                : new TrechoTexto { Texto = m.Value });

            pos = m.Index + m.Length;
        }

        if (pos < texto.Length)
        {
            partes.Add(new TrechoTexto { Texto = texto[pos..] });
        }

        return partes;
    }

    // Igual a DividirEmTrechosAsync, mas pra uso pelo MarkdownConversor: recebe
    // um trecho de texto que já sabe se está em negrito/itálico/código (herdado
    // da formatação Markdown ao redor) e carimba isso em cada TrechoTexto de
    // texto resultante (as fórmulas viram imagem de qualquer forma, então não
    // faz sentido "negrito" ou "itálico" nelas). Se o texto não tiver nenhuma
    // fórmula, devolve ele inteiro como um único trecho — diferente de
    // DividirEmTrechosAsync, que devolve lista vazia nesse caso (seu contrato
    // é "vazio = nada a fazer, use o texto bruto direto").
    public static async Task<List<TrechoTexto>> DividirComFormatacaoAsync(string texto, bool negrito, bool italico, bool codigoInline)
    {
        var partes = await DividirEmTrechosAsync(texto);
        if (partes.Count == 0)
        {
            partes.Add(new TrechoTexto { Texto = texto });
        }

        foreach (var parte in partes)
        {
            if (parte.Texto is not null)
            {
                parte.Negrito = negrito;
                parte.Italico = italico;
                parte.CodigoInline = codigoInline;
            }
        }

        return partes;
    }

    private static async Task<byte[]?> RenderizarAsync(string latex)
    {
        try
        {
            var url = $"https://latex.codecogs.com/png.image?\\dpi{{150}}\\bg{{white}}{Uri.EscapeDataString(latex)}";
            using var resposta = await Http.GetAsync(url);
            return resposta.IsSuccessStatusCode ? await resposta.Content.ReadAsByteArrayAsync() : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class TrechoTexto
{
    public string? Texto { get; set; }
    public byte[]? ImagemPng { get; set; }

    // Formatação herdada do Markdown ao redor (só fazem sentido quando Texto
    // não é nulo — um trecho de imagem de fórmula nunca é negrito/itálico/código).
    public bool Negrito { get; set; }
    public bool Italico { get; set; }
    public bool CodigoInline { get; set; }
}
