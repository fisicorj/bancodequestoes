using BancoQuestoes.CartaoResposta;
using BancoQuestoes.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace BancoQuestoes.Exportacao;

// Gera o PDF dos cartões resposta: 1 página por aluno, com QR Code (identifica o aluno na
// leitura da foto), 4 marcadores de canto (correção de perspectiva) e a grade de bolhas A-E
// por questão. Usa PdfSharp "cru" (não MigraDoc) porque a leitura da foto depende de saber a
// posição EXATA de cada elemento — ver CartaoRespostaLayout, única fonte de verdade dessas
// coordenadas, compartilhada com LeitorCartaoRespostaService.
public static class CartaoRespostaPdfExporter
{
    private static readonly XFont FonteTitulo = new("Arial", 13, XFontStyleEx.Bold);
    private static readonly XFont FonteNormal = new("Arial", 10);
    private static readonly XFont FonteBolha = new("Arial", 9, XFontStyleEx.Bold);
    private static readonly XFont FonteRodape = new("Arial", 7);

    public static byte[] GerarLote(
        string disciplinaRotulo,
        string? tipoRotulo,
        string turmaRotulo,
        List<QuestaoCartaoInfo> questoes,
        List<(string AlunoNome, string Token)> cartoes)
    {
        using var document = new PdfDocument();

        foreach (var cartao in cartoes)
        {
            DesenharPaginasDoCartao(document, disciplinaRotulo, tipoRotulo, turmaRotulo, cartao.AlunoNome, cartao.Token, questoes);
        }

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    // Um cartão só: usado quando o professor precisa reimprimir o de um aluno específico
    // (extraviou, rasurou etc.) sem reimprimir a turma inteira.
    public static byte[] GerarUnico(
        string disciplinaRotulo,
        string? tipoRotulo,
        string turmaRotulo,
        string alunoNome,
        string token,
        List<QuestaoCartaoInfo> questoes)
    {
        using var document = new PdfDocument();
        DesenharPaginasDoCartao(document, disciplinaRotulo, tipoRotulo, turmaRotulo, alunoNome, token, questoes);

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    private static void DesenharPaginasDoCartao(
        PdfDocument document,
        string disciplinaRotulo,
        string? tipoRotulo,
        string turmaRotulo,
        string alunoNome,
        string token,
        List<QuestaoCartaoInfo> questoes)
    {
        var qrBytes = GerarQrPng(token);
        using var qrStream = new MemoryStream(qrBytes);
        using var qrImage = XImage.FromStream(qrStream);

        var porPagina = CartaoRespostaLayout.QuestoesPorPagina();
        var totalPaginas = (int)Math.Ceiling(questoes.Count / (double)porPagina);

        for (var numeroPagina = 0; numeroPagina < totalPaginas; numeroPagina++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(CartaoRespostaLayout.LarguraPagina);
            page.Height = XUnit.FromPoint(CartaoRespostaLayout.AlturaPagina);

            using var gfx = XGraphics.FromPdfPage(page);

            DesenharMarcadores(gfx);
            var (qrX, qrY) = CartaoRespostaLayout.QrCabecalho();
            gfx.DrawImage(qrImage, qrX, qrY, CartaoRespostaLayout.TamanhoQr, CartaoRespostaLayout.TamanhoQr);

            DesenharCabecalho(gfx, disciplinaRotulo, tipoRotulo, turmaRotulo, alunoNome, numeroPagina + 1, totalPaginas);

            var questoesDaPagina = questoes.Skip(numeroPagina * porPagina).Take(porPagina).ToList();
            DesenharCabecalhoColunas(gfx, questoesDaPagina);
            DesenharGradeBolhas(gfx, questoesDaPagina);

            gfx.DrawString($"Token: {token}", FonteRodape, XBrushes.Gray,
                new XRect(CartaoRespostaLayout.Margem, CartaoRespostaLayout.AlturaPagina - 20, 300, 14), XStringFormats.CenterLeft);
        }
    }

    private static void DesenharMarcadores(XGraphics gfx)
    {
        var tamanho = CartaoRespostaLayout.TamanhoMarcador;
        foreach (var (x, y) in new[]
                 {
                     CartaoRespostaLayout.MarcadorTopoEsquerdo(),
                     CartaoRespostaLayout.MarcadorTopoDireito(),
                     CartaoRespostaLayout.MarcadorBaseEsquerdo(),
                     CartaoRespostaLayout.MarcadorBaseDireito(),
                 })
        {
            gfx.DrawRectangle(XBrushes.Black, x, y, tamanho, tamanho);
        }
    }

    // Texto do cabeçalho vai ABAIXO do QR (centralizado no topo) e dos marcadores, ocupando a
    // largura inteira entre os marcadores esquerdo e direito — layout mudou porque o QR não
    // pode mais ficar perto de nenhum canto (ver comentário em CartaoRespostaLayout.TamanhoQr).
    // Mostra só o que o professor pediu: aluno, turma, disciplina e tipo de prova — nada de
    // título completo da prova (que pode ser longo e não cabe bem numa única linha central).
    private static void DesenharCabecalho(XGraphics gfx, string disciplinaRotulo, string? tipoRotulo, string turmaRotulo, string alunoNome, int pagina, int totalPaginas)
    {
        var x = CartaoRespostaLayout.Margem + CartaoRespostaLayout.TamanhoMarcador + 10;
        var largura = CartaoRespostaLayout.LarguraPagina - 2 * x;
        var y = CartaoRespostaLayout.Margem + CartaoRespostaLayout.TamanhoQr + 10;

        gfx.DrawString("CARTÃO RESPOSTA", FonteTitulo, XBrushes.Black, new XRect(x, y, largura, 18), XStringFormats.TopCenter);
        gfx.DrawString($"Aluno: {alunoNome}", FonteNormal, XBrushes.Black, new XRect(x, y + 20, largura, 14), XStringFormats.TopCenter);

        var linhaTurmaDisciplina = string.IsNullOrEmpty(disciplinaRotulo)
            ? $"Turma: {turmaRotulo}"
            : $"Turma: {turmaRotulo}   —   {disciplinaRotulo}";
        gfx.DrawString(linhaTurmaDisciplina, FonteNormal, XBrushes.Black, new XRect(x, y + 36, largura, 14), XStringFormats.TopCenter);

        if (!string.IsNullOrEmpty(tipoRotulo))
        {
            gfx.DrawString($"Tipo: {tipoRotulo}", FonteRodape, XBrushes.Gray, new XRect(x, y + 52, largura, 13), XStringFormats.TopCenter);
        }

        if (totalPaginas > 1)
        {
            gfx.DrawString($"Página {pagina}/{totalPaginas}", FonteRodape, XBrushes.Gray, new XRect(x, y + 66, largura, 13), XStringFormats.TopCenter);
        }

        // Nada de "bolha" no texto — instrução direta e objetiva. Fica numa faixa própria
        // (TopoGradeBolhas - 42 a -28) SEM tocar nem no cabeçalho acima nem no cabeçalho de
        // colunas A/B/C/D logo abaixo (DesenharCabecalhoColunas, em TopoGradeBolhas - 22) —
        // esse encavalamento foi o bug reportado (texto grudado nos rótulos das colunas).
        gfx.DrawString(
            "Marque apenas UMA opção por questão, usando caneta azul ou preta. Não rasure — em caso de erro, peça um cartão novo.",
            FonteRodape, XBrushes.Black,
            new XRect(CartaoRespostaLayout.Margem, CartaoRespostaLayout.TopoGradeBolhas - 42, CartaoRespostaLayout.LarguraPagina - 2 * CartaoRespostaLayout.Margem, 14),
            XStringFormats.TopLeft);
    }

    // Rótulos A/B/C/D(/E) das colunas, desenhados UMA VEZ por página (não mais repetidos
    // acima de cada bolha de cada questão — era isso que colidia com a instrução acima).
    private static void DesenharCabecalhoColunas(XGraphics gfx, List<QuestaoCartaoInfo> questoesDaPagina)
    {
        var maxAlternativas = questoesDaPagina.Count == 0 ? 0 : questoesDaPagina.Max(q => q.QuantidadeAlternativas);
        var r = CartaoRespostaLayout.RaioBolha;

        for (var alt = 0; alt < maxAlternativas; alt++)
        {
            var (cx, _) = CartaoRespostaLayout.CentroBolha(0, alt);
            gfx.DrawString(((char)('A' + alt)).ToString(), FonteBolha, XBrushes.Black,
                new XRect(cx - r, CartaoRespostaLayout.TopoGradeBolhas - 22, r * 2, 12), XStringFormats.Center);
        }
    }

    private static void DesenharGradeBolhas(XGraphics gfx, List<QuestaoCartaoInfo> questoes)
    {
        for (var indiceQuestao = 0; indiceQuestao < questoes.Count; indiceQuestao++)
        {
            var questao = questoes[indiceQuestao];
            var yLabel = CartaoRespostaLayout.LinhaY(indiceQuestao);
            gfx.DrawString($"{questao.Ordem + 1}.", FonteBolha, XBrushes.Black,
                new XRect(CartaoRespostaLayout.Margem + 10, yLabel - 7, 40, 14), XStringFormats.CenterLeft);

            for (var alt = 0; alt < questao.QuantidadeAlternativas; alt++)
            {
                var (cx, cy) = CartaoRespostaLayout.CentroBolha(indiceQuestao, alt);
                var r = CartaoRespostaLayout.RaioBolha;
                gfx.DrawEllipse(XPens.Black, cx - r, cy - r, r * 2, r * 2);
            }
        }
    }

    private static byte[] GerarQrPng(string token)
    {
        using var generator = new QRCodeGenerator();
        using var dados = generator.CreateQrCode(token, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(dados);
        var pngBruto = qrCode.GetGraphic(10);

        // O PNG que o QRCoder gera (provavelmente indexado/monocromático) não é reconhecido
        // pelo decodificador PNG próprio do PdfSharp 6.x (XImage.FromStream lança "Unsupported
        // image format." nele, mesmo sendo um PNG válido — problema conhecido do PdfSharp com
        // saída do QRCoder). Recodificar como PNG RGB de 8 bits "comum" via ImageSharp resolve.
        using var imagem = Image.Load<Rgba32>(pngBruto);
        using var destino = new MemoryStream();
        imagem.Save(destino, new PngEncoder { ColorType = PngColorType.Rgb, BitDepth = PngBitDepth.Bit8 });
        return destino.ToArray();
    }
}
