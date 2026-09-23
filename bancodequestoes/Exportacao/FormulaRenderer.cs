using System.Text.RegularExpressions;

namespace BancoQuestoes.Exportacao;

// Fórmulas LaTeX ($...$/$$...$$) viram imagem via serviço público (CodeCogs),
// já que DOCX/PDF não têm motor de composição matemática; se a chamada falhar, mantém o LaTeX bruto.
public static class FormulaRenderer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private static readonly Regex FormulaRegex = new(@"\$\$(.+?)\$\$|\$(?!\$)(.+?)\$", RegexOptions.Singleline | RegexOptions.Compiled);

    // Divide um texto em trechos alternados de texto puro e imagem de fórmula,
    // na ordem em que aparecem — permite montar um parágrafo com fórmulas inline.
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

    // Igual a DividirEmTrechosAsync, mas carimba a formatação herdada (negrito/itálico/código)
    // em cada trecho de TEXTO e sempre devolve ao menos um trecho.
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
