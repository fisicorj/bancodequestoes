using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BancoQuestoes.Importacao;

// Isola TODA chamada à biblioteca de renderização PDF -> imagem (PDFtoImage) —
// nenhum outro arquivo deve referenciar esse namespace diretamente.
public interface IPaginaPdfRenderizador
{
    // Renderiza só as páginas pedidas (1-based); páginas que falharem ficam
    // ausentes do resultado, nunca lança exceção.
    IReadOnlyDictionary<int, byte[]> RenderizarPaginas(byte[] pdfBytes, IEnumerable<int> numerosPagina);
}

// A app só roda em Windows/Linux/macOS (Blazor Server, sem publish pra
// browser/Android/iOS) — declarar isso é o bastante pro analisador (CA1416)
// reconhecer que PDFtoImage.Conversion.ToImage está coberto (dispensa pragma).
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PaginaEnadeRenderizador : IPaginaPdfRenderizador
{
    // PDFium não é thread-safe — só um PDF por vez no processo inteiro; este lock
    // serializa renderizações concorrentes (aceitável, importação não é rota de alto tráfego).
    private static readonly SemaphoreSlim PdfiumLock = new(1, 1);

    // Bem abaixo do padrão da biblioteca (300 DPI): são snapshots de
    // conferência/auditoria, não material de impressão — 110 DPI mantém a página legível com PNG pequeno.
    private const int DpiSnapshot = 110;

    // Segunda trava além do DPI baixo: nunca renderiza mais páginas que isto num
    // lote — além do limite, a questão continua importável, só sem snapshot.
    private const int MaximoPaginasPorLote = 60;

    // Opcional (default null) pra não quebrar quem instancia direto (ex.: testes)
    // fora do container de DI, que injeta o logger de verdade via Program.cs.
    private readonly ILogger<PaginaEnadeRenderizador>? logger;

    public PaginaEnadeRenderizador(ILogger<PaginaEnadeRenderizador>? logger = null)
    {
        this.logger = logger;
    }

    public IReadOnlyDictionary<int, byte[]> RenderizarPaginas(byte[] pdfBytes, IEnumerable<int> numerosPagina)
    {
        var resultado = new Dictionary<int, byte[]>();

        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return resultado;
        }

        var paginasUnicas = numerosPagina
            .Where(n => n >= 1)
            .Distinct()
            .OrderBy(n => n)
            .Take(MaximoPaginasPorLote);

        PdfiumLock.Wait();
        try
        {
            foreach (var numeroPagina in paginasUnicas)
            {
                try
                {
                    // PdfPig.Page.Number é 1-based; "page" de Conversion.ToImage é 0-based, daí o "- 1".
                    var options = new PDFtoImage.RenderOptions(Dpi: DpiSnapshot);
                    using var bitmap = PDFtoImage.Conversion.ToImage(pdfBytes, numeroPagina - 1, null, options);

                    using var memoria = new MemoryStream();
                    bitmap.Encode(memoria, SKEncodedImageFormat.Png, 90);
                    resultado[numeroPagina] = memoria.ToArray();
                }
                catch (Exception ex)
                {
                    // Ver comentário na interface: falha isolada de uma
                    // página não pode derrubar a importação inteira.
                    logger?.LogWarning(ex, "Falha ao renderizar snapshot da página {Pagina} do PDF ENADE — página ficará sem snapshot.", numeroPagina);
                }
            }
        }
        finally
        {
            PdfiumLock.Release();
        }

        return resultado;
    }
}
