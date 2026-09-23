using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BancoQuestoes.Importacao;

// Extração de texto SIMPLES de PDF pra alimentar geração de questão por IA — só
// texto corrido na ordem das páginas, bem mais simples que EnadeProvaParser.
public static class ExtratorTextoPdf
{
    // Lança se o PDF não puder ser aberto (criptografado, corrompido) — quem
    // chama decide a mensagem amigável, mesmo padrão dos outros parsers.
    public static string Extrair(byte[] pdfBytes)
    {
        using var documento = PdfDocument.Open(pdfBytes);
        var sb = new StringBuilder();
        foreach (var pagina in documento.GetPages())
        {
            sb.AppendLine(TextoSeguro(pagina));
        }

        return sb.ToString();
    }

    // Mesmo cuidado de EnadeProvaParser.SeguroPageText: melhor perder o texto
    // de UMA página malformada do que a extração inteira.
    private static string TextoSeguro(Page pagina)
    {
        try
        {
            return pagina.Text;
        }
        catch
        {
            return "";
        }
    }
}
