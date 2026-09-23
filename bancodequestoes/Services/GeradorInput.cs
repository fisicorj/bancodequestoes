using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Estado do card "Gerar prova automaticamente", consumido por
// GeradorProvaService.Sortear e variantes.
public sealed class GeradorInput
{
    public bool PermiteMultiplaEscolha { get; set; } = true;
    public bool PermiteDiscursiva { get; set; } = true;
    public bool PermiteCertoErrado { get; set; } = true;
    public bool PermiteAssociacao { get; set; } = true;
    public bool PermiteRespostaBreve { get; set; } = true;
    public bool PermiteNumerica { get; set; } = true;
    public bool PermiteLacunas { get; set; } = true;

    // Tipos marcados agora — usada tanto na prévia de "Disponíveis" quanto
    // no sorteio de verdade, pra nunca divergir entre as duas.
    public List<TipoQuestao> TiposPermitidos()
    {
        var tipos = new List<TipoQuestao>();
        if (PermiteMultiplaEscolha) tipos.Add(TipoQuestao.MultiplaEscolha);
        if (PermiteDiscursiva) tipos.Add(TipoQuestao.Discursiva);
        if (PermiteCertoErrado) tipos.Add(TipoQuestao.CertoErrado);
        if (PermiteAssociacao) tipos.Add(TipoQuestao.Associacao);
        if (PermiteRespostaBreve) tipos.Add(TipoQuestao.RespostaBreve);
        if (PermiteNumerica) tipos.Add(TipoQuestao.Numerica);
        if (PermiteLacunas) tipos.Add(TipoQuestao.Lacunas);
        return tipos;
    }

    // Blueprint (Assunto/Bloom/Item): chave = Id (ou NivelBloom), valor = %.
    // PercPorAssunto precisa somar 100; os demais podem ficar vazios.
    public Dictionary<int, int> PercPorAssunto { get; set; } = new();
    public Dictionary<NivelBloom, int> PercPorBloom { get; set; } = new();

    // Só usado no Modo BlueprintCurricular (ItemMatrizReferenciaId -> %).
    public Dictionary<int, int> PercPorItem { get; set; } = new();

    public int Quantidade { get; set; } = 10;
    public int PercFacil { get; set; } = 30;
    public int PercMedia { get; set; } = 50;
    public int PercDificil { get; set; } = 20;

    public decimal? ValorTotal { get; set; }

    public bool EvitarRecentes { get; set; }
    public CriterioEvitarRecentes CriterioRecentes { get; set; } = CriterioEvitarRecentes.PorMeses;
    public int ValorCriterioRecentes { get; set; } = 2;

    // Farol de qualidade como filtro do sorteio, não só exibição —
    // "Qualquer" não filtra nada.
    public QualidadeMinima QualidadeMinima { get; set; } = QualidadeMinima.Qualquer;

    // Quando marcado, a tabela de Tipos vira cota EXATA por tipo
    // (QuantidadePorTipo) em vez de só "pode usar".
    public bool DefinirQuantidadePorTipo { get; set; }

    // Populado/usado só quando DefinirQuantidadePorTipo está marcado.
    public Dictionary<TipoQuestao, int> QuantidadePorTipo { get; set; } = new();

    // Três modos mutuamente exclusivos: "usar os itens marcados" tinha dois
    // significados (filtro OU / blueprint por cota), daí o enum.
    public ModoAlinhamentoCurricular ModoAlinhamentoCurricular { get; set; } = ModoAlinhamentoCurricular.SemMatriz;

    // Usado só no Modo FiltroCurricular — itens que a questão precisa
    // satisfazer pelo menos um (lógica OU).
    public HashSet<int> ItemMatrizIdsDesejados { get; set; } = new();

    // Escopo da prova (Disciplina/Multidisciplinar/Curso, ver EscopoProva.cs).
    // Default Disciplina com DisciplinaIds vazio preserva o comportamento de sempre.
    public EscopoProvaConfig Escopo { get; set; } = new();

    // Distribuição opcional por Disciplina (só no Escopo Multidisciplinar);
    // vazio usa o sorteio de sempre, com entradas SortearPorDisciplina assume.
    public Dictionary<int, int> DistribuicaoDisciplinas { get; set; } = new();
}

// Os três modos de Alinhamento Curricular no gerador, mutuamente exclusivos.
public enum ModoAlinhamentoCurricular
{
    // Comportamento de sempre — não interfere no sorteio nem na tela.
    SemMatriz,

    // Filtro OU sobre o pool de candidatas antes do sorteio normal — nunca
    // garante cobrir todos os itens marcados (a UI precisa deixar isso claro).
    FiltroCurricular,

    // Cada item marcado tem seu % da Quantidade total, com tentativa de cota
    // exata; substitui os percentuais de Assunto/Dificuldade/Bloom enquanto ativo.
    BlueprintCurricular,
}

// Faixa mínima de qualidade pro sorteio — limiares consistentes com as
// cores do farol em QualidadeResultado.CorFarol.
public enum QualidadeMinima
{
    Qualquer,
    Regular,
    Boa,
    Excelente,
}

public static class QualidadeMinimaExtensions
{
    public static int PercentualMinimo(this QualidadeMinima qualidade) => qualidade switch
    {
        QualidadeMinima.Regular => 50,
        QualidadeMinima.Boa => 80,
        QualidadeMinima.Excelente => 95,
        _ => 0,
    };

    public static string Rotulo(this QualidadeMinima qualidade) => qualidade switch
    {
        QualidadeMinima.Qualquer => "Qualquer",
        QualidadeMinima.Regular => "Regular ou superior",
        QualidadeMinima.Boa => "Boa qualidade",
        QualidadeMinima.Excelente => "Excelente",
        _ => qualidade.ToString(),
    };
}

// "Por período": exclui usadas nos últimos N meses. "Por quantidade de
// provas": exclui usadas em qualquer uma das N provas mais recentes.
public enum CriterioEvitarRecentes
{
    PorMeses,
    PorQuantidadeProvas,
}

// Números do card "Diagnóstico do banco" — top-level pra
// GeradorAutomatico.razor também poder referenciar.
public sealed class DiagnosticoBanco
{
    public int Total { get; init; }
    public int Faceis { get; init; }
    public int Medias { get; init; }
    public int Dificeis { get; init; }
    public int UsadasRecentemente { get; init; }
    public int SemBloom { get; init; }
    public int QualidadeBaixa { get; init; }
    public bool BlueprintAtendivel { get; init; }
}

// Uma linha do card "Resultado: aderência ao blueprint".
public sealed class LinhaAderencia
{
    public required string Rotulo { get; init; }
    public required int PlanejadoPercentual { get; init; }
    public required int ObtidoPercentual { get; init; }
    public required int ObtidoQuantidade { get; init; }
    public bool Atende => Math.Abs(PlanejadoPercentual - ObtidoPercentual) <= 1;
}

// Uma linha do card "Planejado × Obtido" do Blueprint Curricular — por Item
// de Matriz, guardando quantidade além de percentual.
public sealed class LinhaBlueprintCurricular
{
    public required ItemMatrizReferencia Item { get; init; }
    public required int PercentualPlanejado { get; init; }
    public required int QuantidadePlanejada { get; init; }
    public required int QuantidadeObtida { get; init; }

    // O algoritmo nunca aloca mais que o planejado — só menos, quando falta
    // questão — então "Atende" é simplesmente "bateu a cota".
    public bool Atende => QuantidadeObtida >= QuantidadePlanejada;
}

// Resultado consolidado de uma geração: questões + avisos + as duas
// aderências possíveis (nunca ambas ao mesmo tempo) + a origem disciplinar real.
public sealed class GeracaoResultado
{
    public required List<Questao> Questoes { get; init; }
    public string? Aviso { get; init; }
    public bool Abortado { get; init; }

    public List<LinhaAderencia> AderenciaAssuntoDificuldadeBloom { get; init; } = new();
    public List<LinhaBlueprintCurricular> AderenciaCurricular { get; init; } = new();

    // Contagem real por Disciplina — sempre calculada no Escopo
    // Multidisciplinar/Curso, mesmo sem distribuição planejada.
    public Dictionary<int, int> DistribuicaoPorDisciplina { get; init; } = new();

    // Planejado x Obtido por Disciplina — vazio sem DistribuicaoDisciplinas configurada.
    public List<LinhaAderencia> AderenciaDisciplina { get; init; } = new();
}

// Diagnóstico pré-geração: por Disciplina (Multidisciplinar) e por Item de
// Matriz (Curso/ENADE), mais o total de questões DISTINTAS elegíveis (nunca soma simples).
public sealed class GeracaoDiagnostico
{
    public int TotalDistintasElegiveis { get; init; }

    public List<LinhaDiagnosticoDisciplina> PorDisciplina { get; init; } = new();
    public List<LinhaDiagnosticoItem> PorItemMatriz { get; init; } = new();

    // Mensagens tipo "C08 solicita 8, mas só 5 disponíveis" geradas das
    // linhas acima quando Disponivel < Solicitado.
    public List<string> Alertas { get; init; } = new();
}

public sealed class LinhaDiagnosticoDisciplina
{
    public required int DisciplinaId { get; init; }
    public required string Nome { get; init; }
    public required int Solicitado { get; init; }
    public required int Disponivel { get; init; }
}

public sealed class LinhaDiagnosticoItem
{
    public required ItemMatrizReferencia Item { get; init; }
    public required int Solicitado { get; init; }
    public required int Disponivel { get; init; }
}
