using System.Linq;

namespace BancoQuestoes.Services;

// Confere se um upload declarado como imagem é REALMENTE uma imagem, olhando os primeiros
// bytes do arquivo (assinatura/"magic bytes"), não o Content-Type que o navegador declarou —
// esse é só um input de formulário como qualquer outro, um upload malicioso pode mandar
// qualquer valor ali. Sem essa checagem, um arquivo HTML/JS enviado com Content-Type
// forjado de "image/png" ficaria salvo e seria servido de volta com esse mesmo
// Content-Type pelos endpoints /questoes/imagem e /instituicoes/logo — abrindo XSS
// armazenado pra quem abrir a URL da imagem direto no navegador.
public static class ValidadorImagem
{
    // Content-Type "canônico" por assinatura, ignorando o que o navegador mandou — assim o
    // endpoint que serve a imagem de volta também usa um valor confiável, nunca o declarado.
    public static string? DetectarContentType(byte[] conteudo)
    {
        if (conteudo.Length >= 8 && conteudo[0] == 0x89 && conteudo[1] == 0x50 && conteudo[2] == 0x4E && conteudo[3] == 0x47)
        {
            return "image/png";
        }
        if (conteudo.Length >= 3 && conteudo[0] == 0xFF && conteudo[1] == 0xD8 && conteudo[2] == 0xFF)
        {
            return "image/jpeg";
        }
        if (conteudo.Length >= 6 && conteudo[0] == 'G' && conteudo[1] == 'I' && conteudo[2] == 'F' && conteudo[3] == '8' && (conteudo[4] == '7' || conteudo[4] == '9') && conteudo[5] == 'a')
        {
            return "image/gif";
        }
        if (conteudo.Length >= 12 && conteudo[0] == 'R' && conteudo[1] == 'I' && conteudo[2] == 'F' && conteudo[3] == 'F'
            && conteudo[8] == 'W' && conteudo[9] == 'E' && conteudo[10] == 'B' && conteudo[11] == 'P')
        {
            return "image/webp";
        }
        if (conteudo.Length >= 2 && conteudo[0] == 'B' && conteudo[1] == 'M')
        {
            return "image/bmp";
        }
        if (EhHeic(conteudo))
        {
            // ImageSharp não decodifica HEIC/HEIF (formato padrão de foto do iPhone) — mas é
            // uma imagem de verdade, não um upload malicioso, então deixa passar aqui pra cair
            // no tratamento específico (mensagem amigável) em LeitorCartaoRespostaService, em
            // vez de ser barrado como "arquivo inválido" já nessa checagem de assinatura.
            return "image/heic";
        }
        return null; // assinatura não reconhecida como imagem — chamador deve rejeitar o upload
    }

    public static bool EhImagemValida(byte[] conteudo) => DetectarContentType(conteudo) is not null;

    // HEIC/HEIF é um contêiner ISO BMFF (mesma família do MP4): 4 bytes de tamanho da caixa,
    // depois "ftyp" e uma "major brand" de 4 letras que identifica o formato específico.
    private static readonly string[] MarcasHeic = { "heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs", "mif1", "msf1" };

    private static bool EhHeic(byte[] conteudo)
    {
        if (conteudo.Length < 12)
        {
            return false;
        }

        if (conteudo[4] != 'f' || conteudo[5] != 't' || conteudo[6] != 'y' || conteudo[7] != 'p')
        {
            return false;
        }

        var marca = System.Text.Encoding.ASCII.GetString(conteudo, 8, 4);
        return MarcasHeic.Contains(marca);
    }
}
