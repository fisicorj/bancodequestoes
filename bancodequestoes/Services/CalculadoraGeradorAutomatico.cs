using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Contexto (somente leitura) para as contas de Diagnostico/Blueprint do
// Gerador Automatico — reune os dados que ProvaForm mantem em memoria e que
// esses calculos leem. Extraido de ProvaForm.razor.cs (Etapa 2 da reducao de
// tamanho do arquivo, complementar ao code-behind da Etapa 1) sem nenhuma
// mudanca de comportamento: cada metodo abaixo e uma copia fiel do que
// existia em ProvaForm, só trocando os campos privados pelo mesmo dado via
// "ctx". ProvaForm continua expondo os mesmos nomes/assinaturas de sempre
// (usados pelo markup e por GeradorAutomatico.razor via [Parameter]/Func<>),
// só que agora como wrappers finos que montam o contexto e delegam pra cá.
public sealed class ContextoCalculoGerador
{
    // Instância injetada (via DI) que ProvaForm já tem — PoolQualificado,
    // DisponiveisPorAssunto/PorItem/PorTipo e DisponiveisDistintasParaConjunto
    // são métodos de instância em GeradorProvaService (não estáticos), ao
    // contrário de DistribuirPorPercentual/AlocarPorItemMaisRaroPrimeiro.
    public required GeradorProvaService GeradorProvaService { get; init; }
    public required GeradorInput Gerador { get; init; }
    public required List<Questao> QuestoesDisponiveis { get; init; }
    public required List<Assunto> AssuntosDisponiveis { get; init; }
    public required List<ItemMatrizReferencia> ItensCurricular { get; init; }
    public required List<QuestaoSelecionada> Selecionadas { get; init; }
    public required Dictionary<int, Questao> QuestoesPorId { get; init; }
    public required HashSet<int> IdsRecentesDiagnostico { get; init; }
}

public static class CalculadoraGeradorAutomatico
{
    public static int TotalPercAssuntos(ContextoCalculoGerador ctx) => ctx.Gerador.PercPorAssunto.Values.Sum();
    public static int TotalPercBloom(ContextoCalculoGerador ctx) => ctx.Gerador.PercPorBloom.Values.Sum();

    // Só calcula a distribuição quando a soma fechar 100, senão os números
    // ficariam sem sentido no meio da digitação.
    public static int TotalPercDificuldade(ContextoCalculoGerador ctx) =>
        ctx.Gerador.PercFacil + ctx.Gerador.PercMedia + ctx.Gerador.PercDificil;

    // Reaproveita o mesmo algoritmo (maior resto) do sorteio real, pra prévia
    // nunca divergir do resultado.
    public static int QuantidadePorDificuldade(ContextoCalculoGerador ctx, Dificuldade nivel)
    {
        if (TotalPercDificuldade(ctx) != 100)
        {
            return 0;
        }

        var percentuais = new Dictionary<Dificuldade, int>
        {
            [Dificuldade.Facil] = ctx.Gerador.PercFacil,
            [Dificuldade.Media] = ctx.Gerador.PercMedia,
            [Dificuldade.Dificil] = ctx.Gerador.PercDificil,
        };
        return GeradorProvaService.DistribuirPorPercentual(percentuais, ctx.Gerador.Quantidade).GetValueOrDefault(nivel);
    }

    // "Disponíveis" da tabela, base do aviso quando o % pedido excede isso; wrapper
    // fino porque GeradorAutomatico.razor o recebe como delegate (Func<int,int>).
    public static int DisponiveisPorAssunto(ContextoCalculoGerador ctx, int assuntoId) =>
        ctx.GeradorProvaService.DisponiveisPorAssunto(ctx.QuestoesDisponiveis, ctx.Gerador, assuntoId, ctx.Gerador.TiposPermitidos());

    // Filtro usa o HashSet de sempre, Blueprint usa as chaves com % marcado,
    // SemMatriz não tem nenhum; centraliza pra não repetir o switch.
    public static HashSet<int> ItensCurricularSelecionados(ContextoCalculoGerador ctx) => ctx.Gerador.ModoAlinhamentoCurricular switch
    {
        ModoAlinhamentoCurricular.FiltroCurricular => ctx.Gerador.ItemMatrizIdsDesejados,
        ModoAlinhamentoCurricular.BlueprintCurricular => ctx.Gerador.PercPorItem.Keys.ToHashSet(),
        _ => new HashSet<int>(),
    };

    // Não é a soma de DisponiveisPorItem (superestima quando itens compartilham
    // questão) — conta cada questão uma vez, se bater com pelo menos um item.
    public static int DisponiveisDistintasParaConjunto(ContextoCalculoGerador ctx) =>
        ctx.GeradorProvaService.DisponiveisDistintasParaConjunto(ctx.QuestoesDisponiveis, ctx.Gerador, ItensCurricularSelecionados(ctx), ctx.Gerador.TiposPermitidos());

    // Diagnóstico pré-geração: "C1 — 24 questões disponíveis".
    public static int DisponiveisPorItem(ContextoCalculoGerador ctx, int itemId) =>
        ctx.GeradorProvaService.DisponiveisPorItem(ctx.QuestoesDisponiveis, ctx.Gerador, itemId, ctx.Gerador.TiposPermitidos());

    // Calculado ao vivo a partir de "selecionadas", então reflete ajustes manuais
    // feitos depois do sorteio, não só o resultado congelado no momento de gerar.
    public static List<LinhaCoberturaItem> CalcularCoberturaCurricular(ContextoCalculoGerador ctx)
    {
        var idsSelecionados = ItensCurricularSelecionados(ctx);
        if (idsSelecionados.Count == 0 || ctx.Selecionadas.Count == 0)
        {
            return new();
        }

        var questoesSelecionadas = ctx.Selecionadas
            .Select(s => ctx.QuestoesPorId.GetValueOrDefault(s.QuestaoId))
            .Where(q => q is not null)
            .Select(q => q!)
            .ToList();

        var resultado = new List<LinhaCoberturaItem>();
        foreach (var id in idsSelecionados)
        {
            var item = ctx.ItensCurricular.FirstOrDefault(i => i.Id == id);
            if (item is null)
            {
                continue;
            }

            var qtd = questoesSelecionadas.Count(q => q.ItensMatriz.Any(i => i.Id == id));
            resultado.Add(new LinhaCoberturaItem { Item = item, Quantidade = qtd });
        }

        return resultado.OrderBy(x => x.Item.Codigo).ToList();
    }

    // --- Blueprint Curricular: diagnóstico + Planejado × Obtido (itens 5/6 do pedido) ---

    // Simula a mesma alocação gulosa do sorteio de verdade, mas com Take em vez
    // de sortear — só pra saber, antes de clicar em Gerar, se cada item bate a cota.
    public static (bool Possivel, Dictionary<int, int> FaltandoPorItem) SimularViabilidadeBlueprint(ContextoCalculoGerador ctx)
    {
        if (ctx.Gerador.PercPorItem.Count == 0 || ctx.Gerador.Quantidade <= 0)
        {
            return (false, new());
        }

        var qtdPlanejada = GeradorProvaService.DistribuirPorPercentual(ctx.Gerador.PercPorItem, ctx.Gerador.Quantidade);
        var tiposPermitidos = ctx.Gerador.TiposPermitidos();
        var pool = ctx.GeradorProvaService.PoolQualificado(ctx.QuestoesDisponiveis, ctx.Gerador).Where(q => tiposPermitidos.Contains(q.TipoQuestao)).ToList();

        var idsJaSelecionados = ctx.Selecionadas.Select(s => s.QuestaoId).ToHashSet();
        var (_, _, faltando) = GeradorProvaService.AlocarPorItemMaisRaroPrimeiro(
            pool,
            qtdPlanejada,
            idsJaSelecionados,
            ordenarExcluindoJaAlocados: true,
            escolher: (elegiveis, qtd) => elegiveis.Take(qtd).ToList());

        return (faltando.Count == 0, faltando);
    }

    // Calculado ao vivo sobre "selecionadas", com a mesma regra de "cada questão
    // só ocupa uma posição" do sorteio de verdade, senão o resumo divergiria.
    public static List<LinhaBlueprintCurricular> CalcularAderenciaBlueprintCurricular(ContextoCalculoGerador ctx)
    {
        if (ctx.Gerador.ModoAlinhamentoCurricular != ModoAlinhamentoCurricular.BlueprintCurricular
            || ctx.Gerador.PercPorItem.Count == 0 || ctx.Selecionadas.Count == 0)
        {
            return new();
        }

        var qtdPlanejada = GeradorProvaService.DistribuirPorPercentual(ctx.Gerador.PercPorItem, ctx.Gerador.Quantidade);

        var questoesSelecionadas = ctx.Selecionadas
            .Select(s => ctx.QuestoesPorId.GetValueOrDefault(s.QuestaoId))
            .Where(q => q is not null)
            .Select(q => q!)
            .ToList();

        // Mesmo núcleo de alocação de SimularViabilidadeBlueprint, sem "já alocados"
        // de fora; usa Take em vez de sortear, só pra contar quantas couberam.
        var (_, obtidoPorItem, _) = GeradorProvaService.AlocarPorItemMaisRaroPrimeiro(
            questoesSelecionadas,
            qtdPlanejada,
            new HashSet<int>(),
            ordenarExcluindoJaAlocados: true,
            escolher: (elegiveis, qtd) => elegiveis.Take(qtd).ToList());

        return qtdPlanejada
            .Where(kv => kv.Value > 0)
            .Select(kv => new { ItemId = kv.Key, Item = ctx.ItensCurricular.FirstOrDefault(i => i.Id == kv.Key), Planejada = kv.Value })
            .Where(x => x.Item is not null)
            .Select(x => new LinhaBlueprintCurricular
            {
                Item = x.Item!,
                PercentualPlanejado = ctx.Gerador.PercPorItem.GetValueOrDefault(x.ItemId),
                QuantidadePlanejada = x.Planejada,
                QuantidadeObtida = obtidoPorItem.GetValueOrDefault(x.ItemId),
            })
            .OrderBy(l => l.Item.Codigo)
            .ToList();
    }

    // Soma do Obtido dividido pela soma do Planejado, em %; nunca passa de 100%
    // porque o algoritmo nunca aloca mais que o planejado por item.
    public static int AderenciaBlueprintCurricularPercentual(ContextoCalculoGerador ctx)
    {
        var linhas = CalcularAderenciaBlueprintCurricular(ctx);
        if (linhas.Count == 0)
        {
            return 0;
        }

        var totalPlanejado = linhas.Sum(l => l.QuantidadePlanejada);
        if (totalPlanejado == 0)
        {
            return 0;
        }

        return (int)Math.Round(100.0 * linhas.Sum(l => l.QuantidadeObtida) / totalPlanejado);
    }

    // Ao vivo, pra tabela mostrar a coluna "Questões" enquanto o professor
    // ainda digita percentuais, sem duplicar o algoritmo do sorteio.
    public static Dictionary<int, int> QtdPlanejadaPorItemBlueprint(ContextoCalculoGerador ctx) =>
        ctx.Gerador.PercPorItem.Count == 0 ? new() : GeradorProvaService.DistribuirPorPercentual(ctx.Gerador.PercPorItem, ctx.Gerador.Quantidade);

    public static bool BlueprintCurricularViavel(ContextoCalculoGerador ctx) => SimularViabilidadeBlueprint(ctx).Possivel;

    // Uma mensagem por item que não teria questões suficientes.
    public static List<string> AvisosBlueprintCurricular(ContextoCalculoGerador ctx)
    {
        if (ctx.Gerador.ModoAlinhamentoCurricular != ModoAlinhamentoCurricular.BlueprintCurricular || ctx.Gerador.PercPorItem.Count == 0)
        {
            return new();
        }

        var (possivel, faltando) = SimularViabilidadeBlueprint(ctx);
        if (possivel)
        {
            return new();
        }

        return faltando
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var item = ctx.ItensCurricular.FirstOrDefault(i => i.Id == kv.Key);
                var rotulo = item is null ? $"item #{kv.Key}" : $"{item.Codigo} — {item.Titulo}";
                return $"{rotulo}: faltam {kv.Value} questão(ões) pra bater a cota planejada.";
            })
            .ToList();
    }

    // Mostrado ao lado dos radios de qualidade mínima, escopado aos
    // tipos/assuntos já marcados no blueprint.
    public static int QuantidadeQualidadeAtende(ContextoCalculoGerador ctx) => PoolDiagnostico(ctx).Count(q =>
        QualidadeQuestao.Calcular(q).Percentual >= ctx.Gerador.QualidadeMinima.PercentualMinimo());

    // Sem aplicar filtro de qualidade mínima: o diagnóstico é informativo sobre
    // tudo que existe, não sobre o que sobra depois de filtrar.
    public static List<Questao> PoolDiagnostico(ContextoCalculoGerador ctx) => ctx.QuestoesDisponiveis
        .Where(q => ctx.Gerador.TiposPermitidos().Contains(q.TipoQuestao) && ctx.Gerador.PercPorAssunto.ContainsKey(q.AssuntoId))
        .ToList();

    // Mostrado antes do professor clicar em gerar, pra ele ver se o blueprint
    // montado tem lastro no banco de questões.
    public static DiagnosticoBanco CalcularDiagnostico(ContextoCalculoGerador ctx)
    {
        var pool = PoolDiagnostico(ctx);
        return new DiagnosticoBanco
        {
            Total = pool.Count,
            Faceis = pool.Count(q => q.Dificuldade == Dificuldade.Facil),
            Medias = pool.Count(q => q.Dificuldade == Dificuldade.Media),
            Dificeis = pool.Count(q => q.Dificuldade == Dificuldade.Dificil),
            UsadasRecentemente = pool.Count(q => ctx.IdsRecentesDiagnostico.Contains(q.Id)),
            SemBloom = pool.Count(q => q.Bloom is null),
            QualidadeBaixa = pool.Count(q => QualidadeQuestao.Calcular(q).Percentual < 50),
            // Aproximação, não um solver de verdade: percentuais fecham 100% e
            // nenhum firewall já apontou falta de questões numa célula.
            BlueprintAtendivel = TotalPercAssuntos(ctx) == 100
                && TotalPercDificuldade(ctx) == 100
                && AvisosDisponibilidade(ctx).Count == 0
                && (!ctx.Gerador.DefinirQuantidadePorTipo || (SomaQuantidadePorTipo(ctx) == ctx.Gerador.Quantidade && AvisosPorTipo(ctx).Count == 0)),
        };
    }

    // Compara Planejado (% configurado) com Obtido (% real em "selecionadas"),
    // calculado ao vivo; tolerância de 1 ponto percentual por arredondamento.
    public static List<LinhaAderencia> CalcularAderenciaBlueprint(ContextoCalculoGerador ctx)
    {
        var linhas = new List<LinhaAderencia>();
        if (ctx.Selecionadas.Count == 0)
        {
            return linhas;
        }

        var questoesSelecionadas = ctx.Selecionadas.Select(s => ctx.QuestoesPorId[s.QuestaoId]).ToList();
        var total = questoesSelecionadas.Count;

        if (TotalPercDificuldade(ctx) == 100)
        {
            foreach (var (nivel, planejado) in new[]
            {
                (Dificuldade.Facil, ctx.Gerador.PercFacil),
                (Dificuldade.Media, ctx.Gerador.PercMedia),
                (Dificuldade.Dificil, ctx.Gerador.PercDificil),
            })
            {
                var qtd = questoesSelecionadas.Count(q => q.Dificuldade == nivel);
                linhas.Add(new LinhaAderencia
                {
                    Rotulo = nivel.Rotulo(),
                    PlanejadoPercentual = planejado,
                    ObtidoPercentual = (int)Math.Round(100.0 * qtd / total),
                    ObtidoQuantidade = qtd,
                });
            }
        }

        if (TotalPercAssuntos(ctx) == 100)
        {
            foreach (var (assuntoId, planejado) in ctx.Gerador.PercPorAssunto)
            {
                var nome = ctx.AssuntosDisponiveis.FirstOrDefault(a => a.Id == assuntoId)?.Nome ?? $"assunto #{assuntoId}";
                var qtd = questoesSelecionadas.Count(q => q.AssuntoId == assuntoId);
                linhas.Add(new LinhaAderencia
                {
                    Rotulo = nome,
                    PlanejadoPercentual = planejado,
                    ObtidoPercentual = (int)Math.Round(100.0 * qtd / total),
                    ObtidoQuantidade = qtd,
                });
            }
        }

        return linhas;
    }

    // Com "Definir quantidade por tipo" marcado, cada tipo permitido vira uma
    // cota exata em vez de só "pode usar" (ver loop em SortearQuestoesAsync).
    public static int DisponiveisPorTipo(ContextoCalculoGerador ctx, TipoQuestao tipo) =>
        ctx.GeradorProvaService.DisponiveisPorTipo(ctx.QuestoesDisponiveis, ctx.Gerador, tipo);

    public static int SomaQuantidadePorTipo(ContextoCalculoGerador ctx) => ctx.Gerador.QuantidadePorTipo.Values.Sum();

    // Igual a DividirIgualmente (em ProvaForm), mas divide uma quantidade
    // inteira (não um percentual de 100), com o resto nas primeiras chaves.
    public static Dictionary<TipoQuestao, int> DividirQuantidadeIgualmente(List<TipoQuestao> tipos, int quantidade)
    {
        var resultado = new Dictionary<TipoQuestao, int>();
        if (tipos.Count == 0)
        {
            return resultado;
        }

        var baseQtd = quantidade / tipos.Count;
        var resto = quantidade - baseQtd * tipos.Count;
        for (var i = 0; i < tipos.Count; i++)
        {
            resultado[tipos[i]] = baseQtd + (i < resto ? 1 : 0);
        }
        return resultado;
    }

    // Mesmo "firewall" de AvisosDisponibilidade, só que por Tipo em vez de
    // Assunto — só roda quando a distribuição por tipo está ativa.
    public static List<string> AvisosPorTipo(ContextoCalculoGerador ctx)
    {
        var avisos = new List<string>();
        if (!ctx.Gerador.DefinirQuantidadePorTipo)
        {
            return avisos;
        }

        foreach (var (tipo, qtd) in ctx.Gerador.QuantidadePorTipo)
        {
            var disponiveis = DisponiveisPorTipo(ctx, tipo);
            if (qtd > disponiveis)
            {
                avisos.Add($"{tipo.Rotulo()}: solicitado {qtd}, disponíveis {disponiveis}.");
            }
        }
        return avisos;
    }

    // Quantas questões desse Assunto o gerador vai buscar, dado o % atual —
    // mesma lógica de QuantidadePorDificuldade, só que pra Assuntos.
    public static int QuantidadePorAssunto(ContextoCalculoGerador ctx, int assuntoId)
    {
        if (TotalPercAssuntos(ctx) != 100 || ctx.Gerador.Quantidade <= 0)
        {
            return 0;
        }

        return GeradorProvaService.DistribuirPorPercentual(ctx.Gerador.PercPorAssunto, ctx.Gerador.Quantidade).GetValueOrDefault(assuntoId);
    }

    // "Criptografia 3 · Firewall 2 · ..." pro card de resumo do blueprint.
    public static string ResumoConteudoBlueprint(ContextoCalculoGerador ctx) => string.Join(" · ", ctx.Gerador.PercPorAssunto.Keys.Select(id =>
        $"{ctx.AssuntosDisponiveis.FirstOrDefault(a => a.Id == id)?.Nome ?? "?"} {QuantidadePorAssunto(ctx, id)}"));

    public static string TiposPermitidosTexto(ContextoCalculoGerador ctx) => string.Join(" · ", ctx.Gerador.TiposPermitidos().Select(t => t.Rotulo()));

    // Assuntos onde a quantidade pedida passa do disponível, mostrados como
    // alerta antes do professor sortear.
    public static List<string> AvisosDisponibilidade(ContextoCalculoGerador ctx)
    {
        var avisos = new List<string>();
        if (TotalPercAssuntos(ctx) != 100)
        {
            return avisos;
        }

        foreach (var (assuntoId, _) in ctx.Gerador.PercPorAssunto)
        {
            var qtd = QuantidadePorAssunto(ctx, assuntoId);
            var disponiveis = DisponiveisPorAssunto(ctx, assuntoId);
            if (qtd > disponiveis)
            {
                var nome = ctx.AssuntosDisponiveis.FirstOrDefault(a => a.Id == assuntoId)?.Nome ?? $"assunto #{assuntoId}";
                avisos.Add($"{nome}: solicitado {qtd}, disponíveis {disponiveis}.");
            }
        }
        return avisos;
    }
}
