namespace BancoQuestoes.Services;

public sealed class AplicacaoProvaInput
{
    public int ProvaId { get; set; }
    public int TurmaId { get; set; }

    // Opcionais: sem limite, o aluno pode responder a qualquer momento enquanto Aberta.
    public DateTime? DataLimite { get; set; }
    public int? TempoLimiteMinutos { get; set; }
}
