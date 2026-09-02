using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Modelo "achatado" com o resultado de UMA questão interpretada de um arquivo
// Aiken ou GIFT — ainda não é uma entidade do banco, só o que a tela de
// pré-visualização e o botão "Importar" precisam.
public class QuestaoImportada
{
    public required string Enunciado { get; set; }
    public required TipoQuestao Tipo { get; set; }

    // Múltipla escolha
    public List<AlternativaImportada> Alternativas { get; set; } = new();

    // Certo/Errado
    public bool? RespostaCertoErrado { get; set; }

    // Resposta breve
    public string? RespostaBreveEsperada { get; set; }

    // Numérica
    public decimal? NumericaEsperada { get; set; }
    public decimal? NumericaTolerancia { get; set; }

    // Associação
    public List<ParAssociacaoImportado> Pares { get; set; } = new();

    // Se algo ficou estranho mas ainda dá pra importar (ex.: resposta breve com
    // mais de uma alternativa aceita — só a primeira é usada), fica um aviso em
    // vez de descartar a questão inteira.
    public string? Aviso { get; set; }

    // Selecionada por padrão na pré-visualização; o usuário pode desmarcar
    // antes de confirmar a importação.
    public bool Selecionada { get; set; } = true;
}

public class AlternativaImportada
{
    public required string Texto { get; set; }
    public bool Correta { get; set; }
}

public class ParAssociacaoImportado
{
    public required string Termo { get; set; }
    public required string Correspondente { get; set; }
}

// Resultado completo de uma importação: questões reconhecidas + erros de
// trechos que não deu pra interpretar (mostrados à parte, sem travar o resto).
public class ResultadoImportacao
{
    public List<QuestaoImportada> Questoes { get; set; } = new();
    public List<string> Erros { get; set; } = new();
}
