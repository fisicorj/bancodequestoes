namespace BancoQuestoes.Models;

// Auditoria de quem alterou o quê numa questão institucional/compartilhada. Só grava os
// campos "principais" (ver QuestaoHistoricoService), não é audit log genérico.
public class QuestaoHistorico
{
    public int Id { get; set; }

    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    // Nullable porque o usuário pode ser excluído depois (SetNull na FK) — nesse
    // caso a tela mostra "um professor" em vez de perder a linha.
    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;

    // Nome do campo já em português, pronto pra exibição (ex.: "Enunciado",
    // "Dificuldade", "Outros dados da questão").
    public required string Campo { get; set; }

    // Nulos quando o campo não tem valor curto o bastante pra mostrar antes/depois
    // (Enunciado, "Outros dados da questão") — a tela só mostra "alterou", sem valores.
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
}
