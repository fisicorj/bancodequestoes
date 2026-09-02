using BancoQuestoes.Models;

namespace BancoQuestoes.Exportacao;

// Modelo "achatado" e sem EF, só com o que os dois exportadores (DOCX e PDF)
// precisam. Mantém a lógica de geração de documento desacoplada do banco.
public sealed class ProvaExportDto
{
    public required string Titulo { get; set; }
    public required string Disciplina { get; set; }
    public string? Professor { get; set; }
    public string? Curso { get; set; }
    public InstituicaoExportDto? Instituicao { get; set; }
    public List<QuestaoExportDto> Questoes { get; set; } = new();

    // Metadados de "banco de provas" (Turmas) — todos opcionais, usados pra
    // enriquecer o cabeçalho do documento exportado quando disponíveis.
    public TipoProva? Tipo { get; set; }
    public string? Turma { get; set; }
    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }

    // Só soma se pelo menos uma questão tiver valor definido — senão fica nulo
    // (prova sem pontuação atribuída ainda).
    public decimal? ValorTotal => Questoes.Any(q => q.Valor is not null)
        ? Questoes.Sum(q => q.Valor ?? 0)
        : null;
}

public sealed class InstituicaoExportDto
{
    public required string Nome { get; set; }
    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Telefone { get; set; }
    public string? Site { get; set; }
    public byte[]? LogoConteudo { get; set; }
    public string? LogoContentType { get; set; }

    // Já quebrada em linhas (uma instrução por item), pronta pra virar bullets
    // no documento exportado.
    public List<string> Instrucoes { get; set; } = new();
}

public sealed class QuestaoExportDto
{
    public required string Enunciado { get; set; }
    public required TipoQuestao Tipo { get; set; }
    public decimal? Valor { get; set; }

    // Texto puro + marcação de qual é a correta, SEM letra fixa — assim dá pra
    // embaralhar a ordem entre versões da prova e recalcular a letra (A, B, C...)
    // na hora de montar o documento, tanto na exportação simples quanto nas variações.
    public List<AlternativaExportDto> Alternativas { get; set; } = new();

    // Só relevante quando Tipo == CertoErrado; usado pra montar o gabarito.
    public bool? RespostaCertoErrado { get; set; }

    // Só relevante quando Tipo == Discursiva. O gabarito compacto por versão
    // (ProvaDocxExporter.CriarTabelaGabarito) mostra "—" pra discursiva de
    // propósito — uma resposta livre não cabe numa célula de tabela —, mas o
    // gabarito COMENTADO (documento corrido, uma questão de cada vez) tem
    // espaço de sobra, então vale mostrar completo ali.
    public string? RespostaEsperadaDiscursiva { get; set; }
    public string? CriterioAvaliacaoDiscursiva { get; set; }

    // Só relevante quando Tipo == RespostaBreve.
    public string? RespostaBreveEsperada { get; set; }

    // Só relevante quando Tipo == Numerica.
    public NumericaExportDto? Numerica { get; set; }

    // Só relevante quando Tipo == Associacao. Termo = coluna A (ordem fixa);
    // Correspondente = a resposta certa daquele termo. OrdemCorrespondentes guarda,
    // pra CADA versão da prova, em que ordem embaralhada a coluna B foi impressa —
    // OrdemCorrespondentes[k] é o índice (em Pares) do Termo cujo Correspondente
    // apareceu como a k-ésima opção (letra A+k) da coluna B. Precisa ficar gravado
    // (em vez de sorteado de novo na hora do gabarito) pra imprimir e gabaritar
    // com o MESMO sorteio.
    public List<ParAssociacaoExportDto> Pares { get; set; } = new();
    public List<int>? OrdemCorrespondentes { get; set; }

    // Só relevante quando Tipo == Lacunas — uma resposta esperada por lacuna,
    // na ordem em que aparecem no Enunciado (que já vem com "___" substituído
    // por marcadores numerados, ver ProvaExportLoader).
    public List<string> LacunasRespostas { get; set; } = new();

    public List<ImagemExportDto> Imagens { get; set; } = new();

    // Enunciado já convertido de Markdown (negrito, itálico, lista, código,
    // tabela) + LaTeX pra uma lista de blocos prontos pra desenhar — ver
    // MarkdownConversor. Os exportadores usam isso em vez de reconstruir o
    // parágrafo a partir de Enunciado direto. Nulo/vazio = Enunciado em branco.
    public List<BlocoMarkdown>? EnunciadoBlocos { get; set; }

    // Resolução/comentário do professor — só aparece no "gabarito comentado"
    // (ProvaDocxExporter/ProvaPdfExporter.GerarGabaritoComentado), nunca na
    // prova em branco que o aluno recebe. Mesmo tratamento de EnunciadoBlocos:
    // já convertido de Markdown+LaTeX, pronto pra desenhar.
    public string? Explicacao { get; set; }
    public List<BlocoMarkdown>? ExplicacaoBlocos { get; set; }
}

public sealed class AlternativaExportDto
{
    public required string Texto { get; set; }
    public bool Correta { get; set; }
}

public sealed class NumericaExportDto
{
    public decimal RespostaEsperada { get; set; }
    public decimal Tolerancia { get; set; }
}

public sealed class ParAssociacaoExportDto
{
    public required string Termo { get; set; }
    public required string Correspondente { get; set; }
}

public sealed class ImagemExportDto
{
    public required byte[] Conteudo { get; set; }
    public required string ContentType { get; set; }

    // Legenda e Alinhamento/Largura afetam o documento impresso; TextoAlternativo
    // não tem equivalente visual em DOCX/PDF (só é usado no HTML da prévia em
    // tela), mas viaja no DTO mesmo assim por consistência com QuestaoImagem.
    public string? Legenda { get; set; }
    public string? TextoAlternativo { get; set; }
    public AlinhamentoImagem? Alinhamento { get; set; }
    public int? LarguraPercentual { get; set; }
}
