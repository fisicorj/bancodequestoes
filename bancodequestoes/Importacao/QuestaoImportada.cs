using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Modelo "achatado" com o resultado de UMA questão interpretada de Aiken/GIFT —
// ainda não é entidade do banco, só o que a pré-visualização/importação precisam.
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

    // Alinhamento Curricular/ENADE: códigos de Item de Matriz capturados de "$CATEGORY:"
    // do GIFT, ainda não resolvidos contra o banco (isso é ImportacaoService.ImportarAsync).
    public List<string> CodigosItemMatriz { get; set; } = new();

    // Preenchido DEPOIS de "Analisar" resolver CodigosItemMatriz contra o Curso
    // escolhido — só pra pré-visualização avisar código não encontrado.
    public List<string> CodigosNaoEncontrados { get; set; } = new();

    // Assunto de destino por questão, capturado de "$ASSUNTO: Disciplina / Assunto"
    // no GIFT; nulo = usa o Assunto único da tela. Resolução real é ImportacaoService.ResolverAssuntosAsync.
    public string? DisciplinaSugerida { get; set; }
    public string? AssuntoSugerido { get; set; }

    // Se algo ficou estranho mas ainda dá pra importar, fica um aviso em vez
    // de descartar a questão inteira.
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

    // Um item por combinação ÚNICA de (DisciplinaSugerida, AssuntoSugerido), preenchido
    // por ImportacaoService.ResolverAssuntosAsync — pede UMA decisão do professor por grupo, não por questão.
    public List<GrupoAssuntoImportacao> GruposAssunto { get; set; } = new();
}

// Combinação Disciplina/Assunto sugerida por "$ASSUNTO:" no GIFT, com o resultado
// da checagem no banco e a decisão do professor quando não existe (ver ImportacaoService).
public class GrupoAssuntoImportacao
{
    public required string Disciplina { get; set; }
    public required string Assunto { get; set; }
    public int QuantidadeQuestoes { get; set; }

    // Resultado da busca por nome (case-insensitive); AssuntoIdEncontrado só é
    // preenchido quando Disciplina E Assunto já existem.
    public int? DisciplinaIdEncontrada { get; set; }
    public int? AssuntoIdEncontrado { get; set; }

    // Alerta de "nome parecido": Assunto sugerido bate com uma DISCIPLINA já existente;
    // quando preenchido, ResolverAssuntosAsync já deixa CriarNovo em false por segurança.
    public int? DisciplinaHomonimaEncontradaId { get; set; }
    public string? DisciplinaHomonimaEncontradaNome { get; set; }

    // Decisão do professor, editável na pré-visualização: usa o existente se
    // achou, senão propõe criar com o nome sugerido.
    public bool CriarNovo { get; set; }
    public string NomeDisciplinaFinal { get; set; } = "";
    public string NomeAssuntoFinal { get; set; } = "";
    public int AssuntoIdEscolhido { get; set; }

    // Estado só de UI (nunca lido por ImportacaoService): filtra o dropdown de
    // "Assunto existente" pela Disciplina escolhida, em vez de listar todos os
    // Assuntos do banco misturados. Pré-preenchido com DisciplinaIdEncontrada
    // quando a busca por nome já achou uma Disciplina (ver ResolverAssuntosAsync).
    public int DisciplinaIdFiltro { get; set; }

    // Preenchido durante ImportarAsync — evita criar a mesma Disciplina/Assunto
    // de novo pra cada questão do grupo no mesmo lote.
    public int? AssuntoIdCriado { get; set; }
}
