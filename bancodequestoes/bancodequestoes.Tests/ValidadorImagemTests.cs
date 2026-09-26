using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa ValidadorImagem: detecção de tipo real por assinatura de bytes (magic
// bytes), nunca pelo Content-Type declarado — é o que impede um upload
// disfarçado de imagem virar XSS armazenado (ver comentário na classe).
public class ValidadorImagemTests
{
    [Fact]
    public void DetectarContentType_Png_Reconhece()
    {
        byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        Assert.Equal("image/png", ValidadorImagem.DetectarContentType(png));
    }

    [Fact]
    public void DetectarContentType_Jpeg_Reconhece()
    {
        byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };
        Assert.Equal("image/jpeg", ValidadorImagem.DetectarContentType(jpeg));
    }

    [Fact]
    public void DetectarContentType_Gif87a_Reconhece()
    {
        byte[] gif = "GIF87a"u8.ToArray();
        Assert.Equal("image/gif", ValidadorImagem.DetectarContentType(gif));
    }

    [Fact]
    public void DetectarContentType_Gif89a_Reconhece()
    {
        byte[] gif = "GIF89a"u8.ToArray();
        Assert.Equal("image/gif", ValidadorImagem.DetectarContentType(gif));
    }

    [Fact]
    public void DetectarContentType_Webp_Reconhece()
    {
        // RIFF <4 bytes de tamanho> WEBP
        byte[] webp = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x00, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };
        Assert.Equal("image/webp", ValidadorImagem.DetectarContentType(webp));
    }

    [Fact]
    public void DetectarContentType_Bmp_Reconhece()
    {
        byte[] bmp = { (byte)'B', (byte)'M', 0x00, 0x00 };
        Assert.Equal("image/bmp", ValidadorImagem.DetectarContentType(bmp));
    }

    // O caso central de segurança: um arquivo HTML/JS com extensão/Content-Type
    // forjados de imagem não tem NENHUMA das assinaturas acima — tem que ser rejeitado.
    [Fact]
    public void DetectarContentType_HtmlDisfarcadoDeImagem_NaoReconhece()
    {
        byte[] htmlMalicioso = "<script>alert(1)</script>"u8.ToArray();
        Assert.Null(ValidadorImagem.DetectarContentType(htmlMalicioso));
    }

    [Fact]
    public void DetectarContentType_ArrayVazio_NaoReconhece()
    {
        Assert.Null(ValidadorImagem.DetectarContentType(Array.Empty<byte>()));
    }

    [Fact]
    public void DetectarContentType_MuitoCurtoPraQualquerAssinatura_NaoReconhece()
    {
        byte[] curto = { 0x89, 0x50 };
        Assert.Null(ValidadorImagem.DetectarContentType(curto));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true)]
    [InlineData(new byte[] { 0x00, 0x01, 0x02, 0x03 }, false)]
    public void EhImagemValida_RefleteDetectarContentType(byte[] conteudo, bool esperado)
    {
        Assert.Equal(esperado, ValidadorImagem.EhImagemValida(conteudo));
    }

    // Foto de iPhone (cartão resposta) — precisa passar na validação mesmo sem o ImageSharp
    // conseguir decodificar depois (ver comentário em ValidadorImagem.EhHeic).
    [Theory]
    [InlineData("heic")]
    [InlineData("mif1")]
    public void DetectarContentType_Heic_Reconhece(string marca)
    {
        var bytes = new byte[12];
        bytes[0] = 0x00; bytes[1] = 0x00; bytes[2] = 0x00; bytes[3] = 0x18;
        "ftyp"u8.ToArray().CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes(marca).CopyTo(bytes, 8);

        Assert.Equal("image/heic", ValidadorImagem.DetectarContentType(bytes));
    }

    [Fact]
    public void DetectarContentType_FtypComMarcaDesconhecida_NaoReconhece()
    {
        var bytes = new byte[12];
        "ftyp"u8.ToArray().CopyTo(bytes, 4);
        "zzzz"u8.ToArray().CopyTo(bytes, 8);

        Assert.Null(ValidadorImagem.DetectarContentType(bytes));
    }
}
