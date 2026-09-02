namespace BancoQuestoes.Exportacao;

// Lê largura/altura de PNG e JPEG direto dos bytes, sem depender de
// System.Drawing (que no ASP.NET Core moderno só é suportado no Windows e a
// Microsoft recomenda evitar em apps servidor). Só serve pra manter a
// proporção da imagem no DOCX exportado — se o formato não for reconhecido,
// quem chama cai num tamanho padrão razoável.
internal static class ImagemUtils
{
    public static (int Largura, int Altura)? LerDimensoes(byte[] bytes)
    {
        // PNG: assinatura de 8 bytes, depois o chunk IHDR traz largura/altura
        // (big-endian) nos bytes 16-23.
        if (bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            var largura = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            var altura = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return (largura, altura);
        }

        // JPEG: percorre os marcadores até achar um SOFn (Start Of Frame), que
        // guarda as dimensões.
        if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < bytes.Length)
            {
                if (bytes[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                var marcador = bytes[i + 1];
                var ehSof = marcador is (>= 0xC0 and <= 0xC3) or (>= 0xC5 and <= 0xC7) or (>= 0xC9 and <= 0xCB) or (>= 0xCD and <= 0xCF);
                if (ehSof)
                {
                    var altura = (bytes[i + 5] << 8) | bytes[i + 6];
                    var largura = (bytes[i + 7] << 8) | bytes[i + 8];
                    return (largura, altura);
                }

                var tamanhoSegmento = (bytes[i + 2] << 8) | bytes[i + 3];
                i += 2 + tamanhoSegmento;
            }
        }

        return null;
    }
}
