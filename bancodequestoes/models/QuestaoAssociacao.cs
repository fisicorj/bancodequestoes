namespace BancoQuestoes.Models;

// Questão de associação: o aluno liga cada "Termo" (coluna A) ao seu
// "Correspondente" certo (coluna B). Guardamos os pares já casados — na hora
// de exportar é que a coluna B é embaralhada e letrada, pra não ficar óbvio
// que o item 1 da coluna A sempre bate com o item 1 da coluna B.
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
