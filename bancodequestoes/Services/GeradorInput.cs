using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Estado do card "Gerar prova automaticamente" — movido de ProvaForm.razor
// pro Service dono da regra de sorteio (ProvaService.Sortear).
public sealed class GeradorInput
{
    public bool PermiteMultiplaEscolha { get; set; } = true;
    public bool PermiteDiscursiva { get; set; } = true;
    public bool PermiteCertoErrado { get; set; } = true;
    public bool PermiteAssociacao { get; set; } = true;
    public bool PermiteRespostaBreve { get; set; } = true;
    public bool PermiteNumerica { get; set; } = true;
    public bool PermiteLacunas { get; set; } = true;

    // Assunto e Bloom (blueprint de avaliação) não moram aqui: o % de cada
    // um depende de quais assuntos/níveis estão marcados na tela naquele
    // momento (uma coleção de tamanho variável, não um campo fixo), então
    // ficam como Dictionary direto em ProvaForm.razor
    // (percPorAssuntoGerador/percPorBloomGerador) — mesmo padrão que
    // assuntosSelecionadosGerador já usava antes disso virar percentual.

    public int Quantidade { get; set; } = 10;
    public int PercFacil { get; set; } = 30;
    public int PercMedia { get; set; } = 50;
    public int PercDificil { get; set; } = 20;

    public decimal? ValorTotal { get; set; }

    public bool EvitarRecentes { get; set; }
    public CriterioEvitarRecentes CriterioRecentes { get; set; } = CriterioEvitarRecentes.PorMeses;
    public int ValorCriterioRecentes { get; set; } = 2;

    // Farol de qualidade (ver QualidadeQuestao) como filtro do sorteio, não só
    // exibição — "Qualquer" não filtra nada (comportamento de sempre).
    public QualidadeMinima QualidadeMinima { get; set; } = QualidadeMinima.Qualquer;

    // Quando marcado, a tabela de Tipos passa a valer como cota EXATA por tipo
    // (ver quantidadePorTipoGerador em ProvaForm.razor), em vez de só "pode
    // usar" — cada tipo permitido é sorteado numa chamada separada a
    // ProvaService.Sortear com sua própria Quantidade.
    public bool DefinirQuantidadePorTipo { get; set; }

    // Alinhamento Curricular / ENADE (opcional/avançado) — itens de Matriz de
    // Referência (Competências, Conteúdos etc., possivelmente de mais de uma
    // matriz do curso ao mesmo tempo). A 2ª rodada de revisão (item 1/2/38 do
    // pedido) separou isso em TRÊS modos explícitos — ver ModoAlinhamentoCurricular
    // — porque "usar os itens marcados" tinha dois significados bem
    // diferentes escondidos atrás do mesmo checklist: um FILTRO (a questão
    // precisa satisfazer PELO MENOS UM item marcado — lógica OU, nunca
    // garante cobrir todos) e um BLUEPRINT (quero X% de C05, Y% de C08 — cada
    // item marcado É uma cota, igual assunto/dificuldade já são). Ter um
    // enum de modo em vez de inferir pelo HashSet.Count evita apresentar o
    // Modo 1 como se garantisse cobertura de todos os itens (o que ele nunca
    // garantiu).
    public ModoAlinhamentoCurricular ModoAlinhamentoCurricular { get; set; } = ModoAlinhamentoCurricular.SemMatriz;

    // Usado SÓ no Modo FiltroCurricular — itens que a questão precisa
    // satisfazer PELO MENOS UM (lógica OU, ver ProvaForm.PoolQualificado).
    // Vazio quando o Modo não é FiltroCurricular.
    public HashSet<int> ItemMatrizIdsDesejados { get; set; } = new();

    // O percentual por item do Modo BlueprintCurricular (ItemMatrizReferenciaId
    // -> %) NÃO mora aqui — mesmo raciocínio já documentado acima pra
    // Assunto/Bloom: é uma coleção de tamanho variável que depende de quais
    // itens estão marcados na tela naquele momento, então fica como
    // Dictionary direto em ProvaForm.razor (percPorItemGerador), mesmo
    // padrão de percPorAssuntoGerador/percPorBloomGerador.
}

// Item 1/2/21/38 da 2ª rodada de revisão: os três modos de Alinhamento
// Curricular no gerador, mutuamente exclusivos.
public enum ModoAlinhamentoCurricular
{
    // Comportamento de sempre — Alinhamento Curricular não interfere no
    // sorteio nem na tela (item 24: "não quebrar o gerador atual" quando a
    // prova não usa Matriz/ENADE).
    SemMatriz,

    // "Utilizar somente questões relacionadas a pelo menos um dos itens
    // selecionados" (item 1 do pedido) — filtro OU sobre o pool de
    // candidatas, ANTES do sorteio por Assunto/Dificuldade/Bloom/Tipo
    // continuar normalmente dentro desse pool já restrito. NUNCA garante que
    // a prova cubra todos os itens marcados (pode sortear 10 questões todas
    // do mesmo item, e zero de outro) — a UI precisa deixar isso explícito
    // (ver GeradorAutomatico.razor), pra não ser lido como o Blueprint.
    FiltroCurricular,

    // "Quero que minha prova tenha esta distribuição de competências/
    // conteúdos" (item 1/2 do pedido) — cada item marcado tem um percentual
    // próprio da Quantidade total (ProvaService.SortearPorBlueprintCurricular),
    // com garantia de tentativa de cota exata por item (não é só um filtro).
    // Substitui os percentuais de Assunto/Dificuldade/Bloom enquanto ativo
    // (simplificação documentada — ver comentário do método no ProvaService).
    BlueprintCurricular,
}

// Faixa mínima de qualidade pra uma questão entrar no sorteio — os limiares
// (Regular/Boa/Excelente) são arbitrários mas consistentes com as cores do
// farol em QualidadeResultado.CorFarol (>=80 já é "success" lá).
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

// "Por período": exclui questões usadas nos últimos N meses (por Data da
// aplicação, ou data de criação da prova quando essa não foi marcada).
// "Por quantidade de provas": exclui questões usadas em qualquer uma das
// N provas mais recentes do professor, independente de quando.
public enum CriterioEvitarRecentes
{
    PorMeses,
    PorQuantidadeProvas,
}

// Números do card "Diagnóstico do banco" (item 5) de ProvaForm.razor — antes
// era "private sealed class" aninhada em ProvaForm; virou top-level (junto
// dos outros tipos-companheiros do gerador, aqui em GeradorInput.cs) pro
// componente GeradorAutomatico.razor também conseguir referenciar, já que um
// tipo privado de uma classe não é visível de outro arquivo.
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

// Uma linha do card "Resultado: aderência ao blueprint" (item 15) de
// ProvaForm.razor — mesmo motivo de DiagnosticoBanco acima: virou top-level
// pro componente GeradorAutomatico.razor conseguir referenciar o tipo.
public sealed class LinhaAderencia
{
    public required string Rotulo { get; init; }
    public required int PlanejadoPercentual { get; init; }
    public required int ObtidoPercentual { get; init; }
    public required int ObtidoQuantidade { get; init; }
    public bool Atende => Math.Abs(PlanejadoPercentual - ObtidoPercentual) <= 1;
}

// Uma linha do card "Resultado: Planejado × Obtido" do Blueprint Curricular
// (item 6 da 2ª rodada de revisão) — igual espírito de LinhaAderencia acima,
// mas por Item de Matriz em vez de Assunto/Dificuldade/Bloom, e guardando
// quantidade (não só percentual) porque o pedido explicitamente quer as
// duas visões (item 6: "Planejado% vs Obtido%... e separadamente
// Planejado-quantidade vs Obtido-quantidade").
public sealed class LinhaBlueprintCurricular
{
    public required ItemMatrizReferencia Item { get; init; }
    public required int PercentualPlanejado { get; init; }
    public required int QuantidadePlanejada { get; init; }
    public required int QuantidadeObtida { get; init; }

    // O algoritmo (ProvaService.SortearPorBlueprintCurricular) nunca aloca
    // MAIS que o planejado pra um item — só menos, quando falta questão —
    // então "Atende" é simplesmente "bateu a cota".
    public bool Atende => QuantidadeObtida >= QuantidadePlanejada;
}
