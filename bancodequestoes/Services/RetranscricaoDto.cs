namespace BancoQuestoes.Services;

// Resultado de uma retranscrição visual (imagem da página -> texto) via IA —
// só o que a IA "leu" na imagem; o professor decide, campo a campo, se aceita.
// Nunca inclui gabarito: a página da prova em si não revela a resposta correta.
public class RetranscricaoDto
{
    public string? Enunciado { get; set; }

    // Só o texto de cada alternativa, na ordem A,B,C... (sem a letra); vazia
    // quando a questão é discursiva ou a IA não reconheceu alternativas.
    public List<string> Alternativas { get; set; } = new();
}
