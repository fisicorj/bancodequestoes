using System.ComponentModel.DataAnnotations;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// DTO do formulário de questão; QuestaoService decide como montar/validar a
// Questao a partir disso (validação por tipo, subclasse certa etc.).
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

    // Metadados de origem ENADE — QuestaoService.ValidarConsistenciaEnadeAsync
    // garante coerência com Origem/Disciplina/AreaCursoIds antes de gravar.
    public SecaoEnade? SecaoEnade { get; set; }
    public string? NumeroOriginal { get; set; }
    public string? CodigoProvaOrigem { get; set; }

    public List<string> Tags { get; set; } = new();

    // Contexto transitório (nunca persistido): escolhe de qual curso vêm as
    // matrizes PPC/Institucional disponíveis. Aplicabilidade acadêmica de verdade é só AreaCursoIds, abaixo.
    public int? CursoContextoMatrizId { get; set; }
    public List<int> ItemMatrizIds { get; set; } = new();

    // Áreas de Curso (nacionais) às quais esta questão é academicamente
    // aplicável — multi-seleção; validado contra AreasCurso antes de gravar.
    public List<int> AreaCursoIds { get; set; } = new();

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

    // Só preenchido se a imagem entrou pelo botão de formatação do
    // enunciado; liga a referência "imagem:pendente:{token}" a esta imagem. Nulo = imagem solta.
    public string? Token { get; set; }
}
