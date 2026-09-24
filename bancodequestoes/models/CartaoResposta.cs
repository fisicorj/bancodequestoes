namespace BancoQuestoes.Models;

// Leitura de cartão resposta: fluxo pra prova IMPRESSA (papel), separado da prova online
// (AplicacaoProva) — o aluno responde no papel, o professor fotografa os cartões preenchidos
// e o sistema lê as bolhas marcadas pra agilizar a correção. Só múltipla escolha (única coisa
// que dá pra ler de bolha marcada); CartaoRespostaService valida isso na criação.
public class CartaoRespostaAplicacao
{
    public int Id { get; set; }

    public int ProvaId { get; set; }
    public Prova? Prova { get; set; }

    public int TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<CartaoAlunoAplicacao> Cartoes { get; set; } = new();
}

// Um cartão por aluno matriculado (ativo) na Turma, gerado de uma vez na criação — mesmo
// padrão do AcessoAlunoAplicacao da prova online. Token é o que vai codificado no QR Code
// impresso no cartão: identifica o aluno na hora de ler a foto, sem precisar de matrícula
// escrita à mão nem de escolher nome numa lista.
public class CartaoAlunoAplicacao
{
    public int Id { get; set; }

    public int CartaoRespostaAplicacaoId { get; set; }
    public CartaoRespostaAplicacao? CartaoRespostaAplicacao { get; set; }

    public int AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    public required string Token { get; set; }

    public StatusCartaoAluno Status { get; set; } = StatusCartaoAluno.PendenteEnvio;

    // Só preenchidos depois que uma foto é processada e a correção é confirmada.
    public decimal? NotaTotal { get; set; }
    public DateTime? CorrigidoEm { get; set; }

    public List<RespostaCartaoQuestao> Respostas { get; set; } = new();
}

// Uma linha por questão do cartão. LetraDetectada é o que o leitor de imagem viu; guardar
// separado de LetraConfirmada preserva o que a leitura automática detectou originalmente
// mesmo que o professor corrija na tela de conferência.
public class RespostaCartaoQuestao
{
    public int Id { get; set; }

    public int CartaoAlunoAplicacaoId { get; set; }
    public CartaoAlunoAplicacao? CartaoAlunoAplicacao { get; set; }

    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    public int Ordem { get; set; }

    // Nulo = nenhuma bolha detectada como marcada (ou a leitura não conseguiu decidir).
    public char? LetraDetectada { get; set; }

    // true quando a leitura achou mais de uma bolha escurecida, ou nenhuma — obriga
    // revisão manual na tela de conferência antes de poder confirmar a correção.
    public bool Ambigua { get; set; }

    // O que vale pra nota de verdade — começa igual à detectada (quando não ambígua), mas o
    // professor pode corrigir na conferência antes de confirmar.
    public char? LetraConfirmada { get; set; }

    public bool Correta { get; set; }
}
