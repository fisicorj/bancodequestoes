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
        return null; // assinatura não reconhecida como imagem — chamador deve rejeitar o upload
    }

    public static bool EhImagemValida(byte[] conteudo) => DetectarContentType(conteudo) is not null;
}
