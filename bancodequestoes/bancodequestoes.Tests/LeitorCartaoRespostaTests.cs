using BancoQuestoes.CartaoResposta;
using BancoQuestoes.Services;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa o pipeline INTEIRO de leitura (QR -> marcadores -> homografia -> bolha) contra uma
// foto SINTÉTICA montada em código, sem perspectiva nenhuma (câmera "perfeitamente
// alinhada") — não substitui testar com fotos de celular de verdade (a leitura de imagem é
// deliberadamente best-effort, sempre com conferência manual depois, conforme decisão
// registrada em CartaoRespostaLayout/LeitorCartaoRespostaService), mas prova que a leitura
// de QR, a localização dos marcadores e a decisão de qual bolha está marcada funcionam de
// ponta a ponta com as coordenadas reais do layout do cartão.
public class LeitorCartaoRespostaTests
{
    // 2 px por ponto PDF — só precisa ser grande o bastante pra sobrar espaço de amostragem
    // dentro de cada bolha/marcador; não precisa bater com nenhum DPI real de impressão.
    private const double Escala = 2.0;

    [Fact]
    public void Ler_FotoSinteticaSemPerspectiva_DecodificaTokenEBolhaMarcada()
    {
        const string token = "TESTE12345";
        var questoes = new List<QuestaoCartaoInfo>
        {
            new() { QuestaoId = 1, Ordem = 0, Valor = 10m, RespostaCorreta = 'A', QuantidadeAlternativas = 4 },
        };

        using var canvas = new Image<Rgb24>(
            (int)(CartaoRespostaLayout.LarguraPagina * Escala),
            (int)(CartaoRespostaLayout.AlturaPagina * Escala),
            Color.White.ToPixel<Rgb24>());

        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorTopoEsquerdo());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorTopoDireito());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorBaseEsquerdo());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorBaseDireito());
        DesenharQr(canvas, token);

        // Marca a bolha da alternativa 'B' (índice 1) da questão 0 — as demais ficam em branco.
        var (cx, cy) = CartaoRespostaLayout.CentroBolha(0, 1);
        PreencherCirculo(canvas, cx * Escala, cy * Escala, CartaoRespostaLayout.RaioBolha * Escala);

        using var ms = new MemoryStream();
        canvas.SaveAsPng(ms);

        var resultado = LeitorCartaoRespostaService.Ler(ms.ToArray(), questoes);

        Assert.True(resultado.Ok, resultado.Erro);
        Assert.Equal(token, resultado.Token);
        var (letra, ambigua) = resultado.RespostasPorQuestao[1];
        Assert.False(ambigua);
        Assert.Equal('B', letra);
    }

    [Fact]
    public void Ler_NenhumaBolhaMarcada_QuestaoFicaAmbigua()
    {
        const string token = "TESTE99999";
        var questoes = new List<QuestaoCartaoInfo>
        {
            new() { QuestaoId = 1, Ordem = 0, Valor = 10m, RespostaCorreta = 'A', QuantidadeAlternativas = 4 },
        };

        using var canvas = new Image<Rgb24>(
            (int)(CartaoRespostaLayout.LarguraPagina * Escala),
            (int)(CartaoRespostaLayout.AlturaPagina * Escala),
            Color.White.ToPixel<Rgb24>());

        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorTopoEsquerdo());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorTopoDireito());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorBaseEsquerdo());
        DesenharMarcador(canvas, CartaoRespostaLayout.MarcadorBaseDireito());
        DesenharQr(canvas, token);

        using var ms = new MemoryStream();
        canvas.SaveAsPng(ms);

        var resultado = LeitorCartaoRespostaService.Ler(ms.ToArray(), questoes);

        Assert.True(resultado.Ok, resultado.Erro);
        var (letra, ambigua) = resultado.RespostasPorQuestao[1];
        Assert.True(ambigua);
        Assert.Null(letra);
    }

    private static void DesenharMarcador(Image<Rgb24> canvas, (double X, double Y) topoEsquerdo)
    {
        var x0 = (int)(topoEsquerdo.X * Escala);
        var y0 = (int)(topoEsquerdo.Y * Escala);
        var tamanho = (int)(CartaoRespostaLayout.TamanhoMarcador * Escala);
        PreencherRetangulo(canvas, x0, y0, tamanho, tamanho);
    }

    private static void DesenharQr(Image<Rgb24> canvas, string token)
    {
        using var generator = new QRCodeGenerator();
        using var dados = generator.CreateQrCode(token, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(dados);
        var qrBytes = qrCode.GetGraphic(10);

        using var qrImage = Image.Load<Rgb24>(qrBytes);
        var tamanho = (int)(CartaoRespostaLayout.TamanhoQr * Escala);
        qrImage.Mutate(ctx => ctx.Resize(tamanho, tamanho));

        var (x, y) = CartaoRespostaLayout.QrCabecalho();
        canvas.Mutate(ctx => ctx.DrawImage(qrImage, new Point((int)(x * Escala), (int)(y * Escala)), 1f));
    }

    private static void PreencherRetangulo(Image<Rgb24> canvas, int x0, int y0, int largura, int altura)
    {
        for (var y = y0; y < y0 + altura && y < canvas.Height; y++)
        {
            for (var x = x0; x < x0 + largura && x < canvas.Width; x++)
            {
                canvas[x, y] = new Rgb24(0, 0, 0);
            }
        }
    }

    private static void PreencherCirculo(Image<Rgb24> canvas, double cx, double cy, double raio)
    {
        var r = (int)Math.Ceiling(raio);
        for (var y = (int)cy - r; y <= (int)cy + r; y++)
        {
            for (var x = (int)cx - r; x <= (int)cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= canvas.Width || y >= canvas.Height)
                {
                    continue;
                }
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= raio * raio)
                {
                    canvas[x, y] = new Rgb24(0, 0, 0);
                }
            }
        }
    }
}
