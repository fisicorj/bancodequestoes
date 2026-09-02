namespace BancoQuestoes.Models;

// Registro de auditoria: quem alterou o quê e quando numa questão. Existe
// principalmente por causa do banco institucional/compartilhado — quando uma
// questão editável por um professor é usada por outros (Institucional ou
// Compartilhada), eles precisam conseguir ver o que mudou, não só confiar
// cegamente no conteúdo atual. Só grava mudança nos campos que
// QuestaoService considera "principais" (ver
// QuestaoService.RegistrarHistoricoDeEdicaoAsync) — não é um audit log
// genérico de toda propriedade do objeto.
public class QuestaoHistorico
{
    public int Id { get; set; }

    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    // Quem editou. Nullable porque o usuário pode ser excluído depois
    // (SetNull na FK, ver ApplicationDbContext) — nesse caso a tela mostra
    // "um professor" em vez de perder a linha do histórico.
    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;

    // Nome do campo já em português, pronto pra exibição (ex.: "Enunciado",
    // "Dificuldade", "Outros dados da questão").
    public required string Campo { get; set; }

    // Nulos quando o campo não tem um valor curto o bastante pra valer a pena
    // mostrar antes/depois (Enunciado, e o balde genérico "Outros dados da
    // questão") — nesses casos a tela só mostra "alterou o enunciado", sem
    // valores.
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
}
