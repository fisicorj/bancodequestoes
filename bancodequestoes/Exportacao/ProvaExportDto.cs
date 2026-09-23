using BancoQuestoes.Models;

namespace BancoQuestoes.Exportacao;

// Modelo achatado e sem EF, só com o que os exportadores (DOCX e PDF) precisam.
public sealed class ProvaExportDto
{
    public required string Titulo { get; set; }

    // Disciplina "principal", só preenchida no Escopo Disciplina; pro cabeçalho
    // impresso use EscopoRotulo abaixo, que resolve os três modos.
    public required string Disciplina { get; set; }

    public string? Professor { get; set; }
    public string? Curso { get; set; }
    public InstituicaoExportDto? Instituicao { get; set; }
    public List<QuestaoExportDto> Questoes { get; set; } = new();

    // Escopo (Disciplina/Multidisciplinar/Curso): só o necessário pro rótulo
    // do cabeçalho impresso (EscopoRotulo), sem regra de geração/validação aqui.
    public TipoEscopoProva TipoEscopo { get; set; } = TipoEscopoProva.Disciplina;
    public List<string> DisciplinasMultidisciplinar { get; set; } = new();
    public TipoMatrizReferencia? MatrizTipo { get; set; }

    // Único lugar que decide o texto do cabeçalho de "matéria" da prova, usado pelos
    // dois exportadores. Simulado ENADE = Escopo Curso + Matriz do tipo ENADE.
    public string EscopoRotulo => TipoEscopo switch
    {
        TipoEscopoProva.Multidisciplinar when DisciplinasMultidisciplinar.Count > 0 =>
            "Multidisciplinar: " + string.Join(", ", DisciplinasMultidisciplinar),
        TipoEscopoProva.Curso when MatrizTipo == TipoMatrizReferencia.ENADE =>
            Curso is { } cursoEnade ? $"Simulado ENADE — {cursoEnade}" : "Simulado ENADE",
        TipoEscopoProva.Curso =>
            Curso is { } cursoLivre ? $"Curso: {cursoLivre}" : "Curso",
        _ => $"Disciplina: {Disciplina}",
    };

    // Metadados de Turma, todos opcionais, pra enriquecer o cabeçalho quando disponíveis.
    public TipoProva? Tipo { get; set; }
    public string? Turma { get; set; }
    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }

    // Só soma se pelo menos uma questão tiver valor definido, senão fica nulo.
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

    // Já quebrada em linhas, pronta pra virar bullets no documento exportado.
    public List<string> Instrucoes { get; set; } = new();
}

public sealed class QuestaoExportDto
{
    public required string Enunciado { get; set; }
    public required TipoQuestao Tipo { get; set; }
    public decimal? Valor { get; set; }

    // Origem/Ano: montam o selo "(ENADE {ano})" no início do enunciado impresso
    // quando Origem == Enade; sem Ano, o selo sai só "(ENADE)".
    public OrigemQuestao Origem { get; set; }
    public int? Ano { get; set; }

    // Texto puro + marcação da correta, sem letra fixa — dá pra embaralhar a ordem
    // entre versões e recalcular a letra na hora de montar o documento.
    public List<AlternativaExportDto> Alternativas { get; set; } = new();

    // Só relevante quando Tipo == CertoErrado; usado pra montar o gabarito.
    public bool? RespostaCertoErrado { get; set; }

    // Só relevante quando Tipo == Discursiva. O gabarito compacto mostra "—" (não
    // cabe numa célula), mas o gabarito comentado tem espaço pra mostrar completo.
    public string? RespostaEsperadaDiscursiva { get; set; }
    public string? CriterioAvaliacaoDiscursiva { get; set; }

    // Só relevante quando Tipo == RespostaBreve.
    public string? RespostaBreveEsperada { get; set; }

    // Só relevante quando Tipo == Numerica.
    public NumericaExportDto? Numerica { get; set; }

    // Tipo == Associacao: Termo é coluna A fixa, Correspondente a resposta certa;
    // OrdemCorrespondentes grava a ordem embaralhada da coluna B por versão.
    public List<ParAssociacaoExportDto> Pares { get; set; } = new();
    public List<int>? OrdemCorrespondentes { get; set; }

    // Só relevante quando Tipo == Lacunas — uma resposta por lacuna, na ordem em
    // que aparecem no Enunciado (já com "___" substituído por marcadores).
    public List<string> LacunasRespostas { get; set; } = new();

    public List<ImagemExportDto> Imagens { get; set; } = new();

    // Enunciado já convertido de Markdown+LaTeX pra blocos prontos pra desenhar
    // (ver MarkdownConversor). Nulo/vazio = Enunciado em branco.
    public List<BlocoMarkdown>? EnunciadoBlocos { get; set; }

    // Resolução do professor: só aparece no gabarito comentado, nunca na prova
    // em branco; mesmo tratamento de EnunciadoBlocos (Markdown+LaTeX já convertido).
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

    // Legenda/Alinhamento/Largura afetam o documento; TextoAlternativo só é usado
    // na prévia em tela, mas viaja no DTO por consistência com QuestaoImagem.
    public string? Legenda { get; set; }
    public string? TextoAlternativo { get; set; }
    public AlinhamentoImagem? Alinhamento { get; set; }
    public int? LarguraPercentual { get; set; }
}
