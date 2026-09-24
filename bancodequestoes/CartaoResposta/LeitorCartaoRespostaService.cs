using BancoQuestoes.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;

namespace BancoQuestoes.CartaoResposta;

// Lê uma foto de cartão resposta preenchido: acha o QR Code (identifica o aluno), acha os 4
// marcadores de canto, calcula a perspectiva da foto em relação ao layout original do PDF
// (CartaoRespostaLayout) e mede o escurecimento de cada bolha esperada.
//
// É DELIBERADAMENTE um leitor "best effort": a tela de conferência (Etapa 7) sempre revisa
// antes de confirmar a nota — então aqui o critério é marcar como AMBÍGUA qualquer leitura
// duvidosa (nenhuma bolha escura o bastante, ou mais de uma) em vez de tentar adivinhar.
public static class LeitorCartaoRespostaService
{
    // Reduzir a foto pra uma largura de trabalho fixa deixa a varredura de pixel rápida e
    // previsível, independente da resolução da câmera do aparelho usado.
    private const int LarguraTrabalho = 1600;

    // Luminância (0-255, 0 = preto) abaixo disso conta como "escuro" nas buscas de marcador
    // e bolha — bolhas preenchidas a caneta ficam bem abaixo disso; papel em branco fica acima.
    private const int LimiarEscuro = 110;

    public static ResultadoLeituraCartao Ler(byte[] fotoBytes, List<QuestaoCartaoInfo> questoes)
    {
        Image<Rgb24> original;
        try
        {
            original = Image.Load<Rgb24>(fotoBytes);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException or InvalidOperationException or NotSupportedException)
        {
            // ImageSharp não decodifica HEIC/HEIF (o formato padrão de foto do iPhone) —
            // sem esse catch, o professor via só a mensagem crua do ImageSharp. iPhones
            // recentes conseguem salvar em JPEG direto se o usuário mudar essa opção,
            // ou dá pra converter a foto antes de enviar (compartilhar como JPEG etc.).
            return ResultadoLeituraCartao.Falha(
                "Não foi possível abrir essa imagem — o formato pode não ser suportado (ex.: fotos HEIC do iPhone). " +
                "No iPhone, vá em Ajustes > Câmera > Formatos e escolha \"Mais Compatível\" (isso salva em JPEG), ou converta a foto antes de enviar.");
        }

        using var descarteOriginal = original;

        // Sempre clona (mesmo sem precisar reduzir) pra sempre ter exatamente UMA imagem
        // pra descartar aqui, sem risco de tentar liberar "original" duas vezes.
        var escala = Math.Min(1.0, LarguraTrabalho / (double)original.Width);
        using var imagem = original.Clone(ctx => ctx.Resize((int)(original.Width * escala), (int)(original.Height * escala)));

        var (token, _) = DecodificarQr(imagem);
        if (token is null)
        {
            return ResultadoLeituraCartao.Falha(
                "Não foi possível localizar o QR Code na foto — tire a foto com o cartão inteiro visível, sem cortar as bordas, e com boa iluminação.");
        }

        // A janela de busca dos marcadores ESQUERDOS precisa ficar mais estreita em X (16%,
        // não 35%) do que a dos direitos: a grade de bolhas começa logo depois do rótulo do
        // número da questão, bem perto da margem esquerda (InicioColunasBolha ≈ 18,5% da
        // largura da página) — uma janela larga o bastante pegaria bolha marcada junto do
        // marcador e distorceria o centroide. À direita não há esse risco: a última coluna de
        // bolha (E) fica bem antes dos 65% da largura, então a janela ampla é segura ali.
        var marcadorTopoEsquerdo = LocalizarMarcador(imagem, regiaoX: (0, 0.16), regiaoY: (0, 0.35));
        var marcadorTopoDireito = LocalizarMarcador(imagem, regiaoX: (0.65, 1.0), regiaoY: (0, 0.35));
        var marcadorBaseEsquerdo = LocalizarMarcador(imagem, regiaoX: (0, 0.16), regiaoY: (0.65, 1.0));
        var marcadorBaseDireito = LocalizarMarcador(imagem, regiaoX: (0.65, 1.0), regiaoY: (0.65, 1.0));

        if (marcadorTopoEsquerdo is null || marcadorTopoDireito is null || marcadorBaseEsquerdo is null || marcadorBaseDireito is null)
        {
            return ResultadoLeituraCartao.Falha(
                "Não foi possível localizar os marcadores pretos dos cantos do cartão na foto — verifique se o cartão inteiro aparece na imagem.");
        }

        // LocalizarMarcador já devolve o CENTROIDE dos pixels escuros da região — pra um
        // marcador sólido isso já É o centro geométrico dele, então usamos o ponto direto
        // (somar TamanhoMarcador/2 de novo aqui deslocaria o ponto pro canto errado).
        var pontoCentroMarcadorTopoEsquerdo = marcadorTopoEsquerdo.Value;
        var pontoCentroMarcadorTopoDireito = marcadorTopoDireito.Value;
        var pontoCentroMarcadorBaseEsquerdo = marcadorBaseEsquerdo.Value;
        var pontoCentroMarcadorBaseDireito = marcadorBaseDireito.Value;

        var origemPagina = new[]
        {
            CentroMarcador(CartaoRespostaLayout.MarcadorTopoEsquerdo()),
            CentroMarcador(CartaoRespostaLayout.MarcadorTopoDireito()),
            CentroMarcador(CartaoRespostaLayout.MarcadorBaseEsquerdo()),
            CentroMarcador(CartaoRespostaLayout.MarcadorBaseDireito()),
        };
        var destinoFoto = new[]
        {
            pontoCentroMarcadorTopoEsquerdo,
            pontoCentroMarcadorTopoDireito,
            pontoCentroMarcadorBaseEsquerdo,
            pontoCentroMarcadorBaseDireito,
        };

        Homografia homografia;
        try
        {
            homografia = Homografia.DeCorrespondencias(origemPagina, destinoFoto);
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoLeituraCartao.Falha($"Não foi possível calcular a posição do cartão na foto: {ex.Message}");
        }

        // Escala aproximada foto/página, só pra dimensionar o raio de amostragem de cada
        // bolha (não usada na localização em si, que já é feita ponto a ponto pela homografia).
        var escalaFotoPorPagina = Distancia(pontoCentroMarcadorTopoEsquerdo, pontoCentroMarcadorBaseDireito)
            / Distancia(origemPagina[0], origemPagina[3]);
        var raioAmostraPixels = Math.Max(3, CartaoRespostaLayout.RaioBolha * escalaFotoPorPagina * 0.7);

        var respostas = new Dictionary<int, (char? Letra, bool Ambigua)>();
        for (var indiceQuestao = 0; indiceQuestao < questoes.Count; indiceQuestao++)
        {
            var questao = questoes[indiceQuestao];
            var escurecimentoPorAlternativa = new List<(char Letra, double Escurecimento)>();

            for (var alt = 0; alt < questao.QuantidadeAlternativas; alt++)
            {
                var (px, py) = CartaoRespostaLayout.CentroBolha(indiceQuestao, alt);
                var pontoFoto = homografia.Aplicar(px, py);
                var escurecimento = MedirEscurecimento(imagem, pontoFoto, raioAmostraPixels);
                escurecimentoPorAlternativa.Add(((char)('A' + alt), escurecimento));
            }

            respostas[questao.QuestaoId] = DecidirLetraMarcada(escurecimentoPorAlternativa);
        }

        return ResultadoLeituraCartao.Sucesso(token, respostas);
    }

    // Escurecimento = fração de pixels "escuros" na janela amostrada (0 = totalmente
    // branco, 1 = totalmente preto) — mais robusto a variação de iluminação entre fotos do
    // que comparar luminância média bruta.
    private static (char? Letra, bool Ambigua) DecidirLetraMarcada(List<(char Letra, double Escurecimento)> alternativas)
    {
        const double limiarMarcada = 0.35;
        const double margemAmbiguidade = 0.12;

        var marcadas = alternativas.Where(a => a.Escurecimento >= limiarMarcada).OrderByDescending(a => a.Escurecimento).ToList();

        if (marcadas.Count == 0)
        {
            return (null, true);
        }

        if (marcadas.Count > 1 && marcadas[0].Escurecimento - marcadas[1].Escurecimento < margemAmbiguidade)
        {
            // Duas ou mais bolhas escuras e sem uma nitidamente mais escura que a outra —
            // não dá pra decidir com segurança automaticamente.
            return (null, true);
        }

        return (marcadas[0].Letra, false);
    }

    private static double MedirEscurecimento(Image<Rgb24> imagem, (double X, double Y) centro, double raio)
    {
        var cx = (int)Math.Round(centro.X);
        var cy = (int)Math.Round(centro.Y);
        var r = (int)Math.Ceiling(raio);

        var total = 0;
        var escuros = 0;

        for (var y = Math.Max(0, cy - r); y <= Math.Min(imagem.Height - 1, cy + r); y++)
        {
            for (var x = Math.Max(0, cx - r); x <= Math.Min(imagem.Width - 1, cx + r); x++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r)
                {
                    continue;
                }

                total++;
                if (Luminancia(imagem[x, y]) < LimiarEscuro)
                {
                    escuros++;
                }
            }
        }

        return total == 0 ? 0 : escuros / (double)total;
    }

    // Procura, dentro de uma janela expressa em FRAÇÃO da imagem (ex.: canto superior
    // esquerdo = x em [0, 0.35], y em [0, 0.35]), o centroide de pixels escuros — é onde o
    // marcador preto impresso deve estar, desde que a foto enquadre o cartão inteiro.
    private static (double X, double Y)? LocalizarMarcador(Image<Rgb24> imagem, (double Min, double Max) regiaoX, (double Min, double Max) regiaoY)
    {
        var x0 = (int)(regiaoX.Min * imagem.Width);
        var x1 = (int)(regiaoX.Max * imagem.Width);
        var y0 = (int)(regiaoY.Min * imagem.Height);
        var y1 = (int)(regiaoY.Max * imagem.Height);

        long somaX = 0, somaY = 0;
        var quantidade = 0;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (Luminancia(imagem[x, y]) < LimiarEscuro)
                {
                    somaX += x;
                    somaY += y;
                    quantidade++;
                }
            }
        }

        // Menos que isso é provavelmente ruído (texto, sombra) e não um marcador de verdade.
        const int minimoPixelsEscuros = 40;
        return quantidade < minimoPixelsEscuros ? null : (somaX / (double)quantidade, somaY / (double)quantidade);
    }

    private static (string? Token, (double X, double Y) Ponto) DecodificarQr(Image<Rgb24> imagem)
    {
        var rgbBytes = new byte[imagem.Width * imagem.Height * 3];
        imagem.CopyPixelDataTo(rgbBytes);

        var luminanceSource = new RGBLuminanceSource(rgbBytes, imagem.Width, imagem.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            },
        };

        var resultado = reader.Decode(luminanceSource);
        if (resultado?.Text is not { Length: > 0 } token || resultado.ResultPoints is not { Length: > 0 } pontos)
        {
            return (null, default);
        }

        var centroX = pontos.Average(p => (double)p.X);
        var centroY = pontos.Average(p => (double)p.Y);
        return (token, (centroX, centroY));
    }

    private static (double X, double Y) CentroMarcador((double X, double Y) topoEsquerdo) =>
        Somar(topoEsquerdo, CartaoRespostaLayout.TamanhoMarcador / 2);

    private static (double X, double Y) Somar((double X, double Y) ponto, double delta) => (ponto.X + delta, ponto.Y + delta);

    private static double Distancia((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static int Luminancia(Rgb24 pixel) =>
        (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
}

public sealed class ResultadoLeituraCartao
{
    public bool Ok { get; private init; }
    public string? Erro { get; private init; }
    public string? Token { get; private init; }
    public Dictionary<int, (char? Letra, bool Ambigua)> RespostasPorQuestao { get; private init; } = new();

    public static ResultadoLeituraCartao Falha(string erro) => new() { Ok = false, Erro = erro };

    public static ResultadoLeituraCartao Sucesso(string token, Dictionary<int, (char? Letra, bool Ambigua)> respostas) =>
        new() { Ok = true, Token = token, RespostasPorQuestao = respostas };
}
