namespace BancoQuestoes.Models;

// Pares já casados são guardados na ordem certa; a coluna B só é embaralhada
// e letrada na exportação, pra não ficar óbvio que item 1 bate com item 1.
public class QuestaoAssociacao : Questao
{
    public List<ParAssociacao> Pares { get; set; } = new();
}

public class ParAssociacao
{
    public int Id { get; set; }

    public int QuestaoAssociacaoId { get; set; }
    public QuestaoAssociacao? Questao { get; set; }

    public required string Termo { get; set; }
    public required string Correspondente { get; set; }

    // Ordem de exibição da coluna A (o Termo). A coluna B é sempre embaralhada
    // na exportação, então não tem "ordem" própria pra guardar.
    public int Ordem { get; set; }
}
