namespace BancoQuestoes.Models;

// Uma "aplicação" de uma Prova a uma Turma pra responder online — CodigoAcesso é o que o
// aluno digita em /responder/{codigo}, sem precisar de login.
public class AplicacaoProva
{
    public int Id { get; set; }

    public int ProvaId { get; set; }
    public Prova? Prova { get; set; }

    public int TurmaId { get; set; }
    public Turma? Turma { get; set; }

    public required string CodigoAcesso { get; set; }

    public StatusAplicacaoProva Status { get; set; } = StatusAplicacaoProva.Aberta;

    // Opcionais: sem limite, o aluno pode responder a qualquer momento enquanto Aberta.
    public DateTime? DataLimite { get; set; }
    public int? TempoLimiteMinutos { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<RespostaProvaOnline> Respostas { get; set; } = new();
}
