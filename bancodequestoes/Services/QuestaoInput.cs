using System.ComponentModel.DataAnnotations;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// "Command"/DTO do formulário de questão — antes vivia como classe privada
// dentro do @code de QuestaoForm.razor; subiu pra cá porque QuestaoService
// agora é quem decide como montar/validar a Questao a partir disso (a
// validação por tipo, a construção da subclasse certa etc. são regra de
// negócio, não coisa de tela). QuestaoForm.razor continua sendo o dono do
// campo `modelo`, só que do tipo público definido aqui.
public sealed class QuestaoInput
{
    public TipoQuestao TipoQuestao { get; set; } = TipoQuestao.MultiplaEscolha;
    public int AssuntoId { get; set; }
    public Dificuldade Dificuldade { get; set; } = Dificuldade.Media;
    public VisibilidadeQuestao Visibilidade { get; set; } = VisibilidadeQuestao.Privada;
    public NivelBloom? Bloom { get; set; }
    public OrigemQuestao Origem { get; set; } = OrigemQuestao.Autoral;
    public int? Ano { get; set; }
    public string? Referencia { get; set; }
    public List<string> Tags { get; set; } = new();

    // Alinhamento Curricular / ENADE — opcional. CursoId escolhe DE QUAL
    // curso as matrizes de referência disponíveis vêm (ver QuestaoForm.razor);
    // ItemMatrizIds pode conter itens de MAIS DE UMA matriz desse curso ao
    // mesmo tempo (item 10 do pedido) e só faz sentido junto de um CursoId
    // preenchido — QuestaoService.CriarAsync/AtualizarAsync ignoram/limpam
    // ids que não pertençam a esse curso, nunca confiando cegamente no que
    // veio do cliente (ver MatrizReferenciaService.ValidarItensDoCursoAsync).
    public int? CursoId { get; set; }
    public List<int> ItemMatrizIds { get; set; } = new();

    [Required(ErrorMessage = "Informe o enunciado.")]
    public string Enunciado { get; set; } = "";

    // Resolução/comentário do professor — opcional, mesma ideia do Enunciado
    // (Markdown + LaTeX), mas sem a rica toolbar de formatação em v1.
    public string? Explicacao { get; set; }

    public bool Ativa { get; set; } = true;

    // Múltipla escolha
    public List<AlternativaInput> Alternativas { get; set; } = new() { new(), new() };
    public int RespostaCorretaIndex { get; set; }

    // Discursiva
    public string RespostaEsperada { get; set; } = "";
    public string? CriterioAvaliacao { get; set; }

    // Certo/Errado
    public bool RespostaCorretaCE { get; set; } = true;

    // Associação
    public List<ParAssociacaoInput> Pares { get; set; } = new() { new(), new() };

    // Resposta breve
    public string RespostaBreveEsperada { get; set; } = "";

    // Numérica
    public decimal? NumericaEsperada { get; set; }
    public decimal? NumericaTolerancia { get; set; }

    // Lacunas
    public List<LacunaInput> Lacunas { get; set; } = new() { new() };
}

public sealed class AlternativaInput
{
    public string Texto { get; set; } = "";
}

public sealed class ParAssociacaoInput
{
    public string Termo { get; set; } = "";
    public string Correspondente { get; set; } = "";
}

public sealed class LacunaInput
{
    public string RespostaEsperada { get; set; } = "";
}

// Imagem já lida para memória no navegador (via InputFile), aguardando o
// Salvar pra virar um QuestaoImagem de verdade e ir pro Postgres.
public sealed class PendenteImagem
{
    public required string NomeArquivo { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Conteudo { get; set; }
    public string? Legenda { get; set; }
    public string? TextoAlternativo { get; set; }
    public AlinhamentoImagem? Alinhamento { get; set; }
    public int? LarguraPercentual { get; set; }
}
