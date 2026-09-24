namespace BancoQuestoes.Models;

// Uma "aplicação" de uma Prova a uma Turma pra responder online — cada aluno matriculado
// recebe um AcessoAlunoAplicacao com CÓDIGO PRÓPRIO (não existe mais um código único
// compartilhado pela turma inteira): impede um aluno abrir a prova no nome de outro só
// porque conhece o código genérico da aplicação.
public class AplicacaoProva
{
    public int Id { get; set; }

    public int ProvaId { get; set; }
    public Prova? Prova { get; set; }

    public int TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public StatusAplicacaoProva Status { get; set; } = StatusAplicacaoProva.Aberta;

    // Opcionais: sem limite, o aluno pode responder a qualquer momento enquanto Aberta.
    public DateTime? DataLimite { get; set; }
    public int? TempoLimiteMinutos { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<AcessoAlunoAplicacao> Acessos { get; set; } = new();
    public List<RespostaProvaOnline> Respostas { get; set; } = new();
}

// Código de acesso individual: gerado um por Aluno matriculado (ativo) na Turma no momento
// em que a AplicacaoProva é criada — é o que o aluno digita em /responder/{codigo}, sem
// precisar de login nem de escolher o próprio nome numa lista.
public class AcessoAlunoAplicacao
{
    public int Id { get; set; }

    public int AplicacaoProvaId { get; set; }
    public AplicacaoProva? AplicacaoProva { get; set; }

    public int AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    public required string CodigoAcesso { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
