using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Motor de geração automática de provas — funções puras sobre listas já
// carregadas (sem ApplicationDbContext).
public class GeradorProvaService
{
    // --- Pool de candidatas ---

    // Pool após o filtro de qualidade mínima — SEM ainda o filtro de item de
    // matriz (contagem por item precisa ver o próprio item sendo contado).
    public List<Questao> PoolPorQualidade(List<Questao> questoesDisponiveis, GeradorInput gerador) =>
        gerador.QualidadeMinima == QualidadeMinima.Qualquer
            ? questoesDisponiveis
            : questoesDisponiveis.Where(q => QualidadeQuestao.Calcular(q).Percentual >= gerador.QualidadeMinima.PercentualMinimo()).ToList();

    // Pool após qualidade mínima E Modo FiltroCurricular (lógica OU). BlueprintCurricular
    // não filtra aqui: a alocação por item é feita em SortearPorBlueprintCurricular.
    public List<Questao> PoolQualificado(List<Questao> questoesDisponiveis, GeradorInput gerador)
    {
        var pool = PoolPorQualidade(questoesDisponiveis, gerador);

        if (gerador.ModoAlinhamentoCurricular != ModoAlinhamentoCurricular.FiltroCurricular || gerador.ItemMatrizIdsDesejados.Count == 0)
        {
            return pool;
        }

        return pool.Where(q => q.ItensMatriz.Any(i => gerador.ItemMatrizIdsDesejados.Contains(i.Id))).ToList();
    }

    // --- Diagnóstico de disponibilidade ---

    // Quantas questões do Assunto existem (já filtradas por tipo, qualidade
    // e Filtro Curricular quando ativo).
    public int DisponiveisPorAssunto(List<Questao> questoesDisponiveis, GeradorInput gerador, int assuntoId, List<TipoQuestao> tiposPermitidos) =>
        PoolQualificado(questoesDisponiveis, gerador).Count(q => q.AssuntoId == assuntoId && tiposPermitidos.Contains(q.TipoQuestao));

    // Conta sobre PoolPorQualidade (sem o filtro de item do gerador), senão
    // marcar outro item mudaria a contagem deste.
    public int DisponiveisPorItem(List<Questao> questoesDisponiveis, GeradorInput gerador, int itemId, List<TipoQuestao> tiposPermitidos) =>
        PoolPorQualidade(questoesDisponiveis, gerador).Count(q => tiposPermitidos.Contains(q.TipoQuestao) && q.ItensMatriz.Any(i => i.Id == itemId));

    // Questões DISTINTAS (cada uma contada uma vez) que batem com pelo menos
    // um item do conjunto — nunca a soma de DisponiveisPorItem (superestimaria).
    public int DisponiveisDistintasParaConjunto(List<Questao> questoesDisponiveis, GeradorInput gerador, HashSet<int> idsItens, List<TipoQuestao> tiposPermitidos)
    {
        if (idsItens.Count == 0)
        {
            return 0;
        }

        return PoolPorQualidade(questoesDisponiveis, gerador).Count(q => tiposPermitidos.Contains(q.TipoQuestao) && q.ItensMatriz.Any(i => idsItens.Contains(i.Id)));
    }

    public int DisponiveisPorTipo(List<Questao> questoesDisponiveis, GeradorInput gerador, TipoQuestao tipo) =>
        PoolQualificado(questoesDisponiveis, gerador).Count(q => q.TipoQuestao == tipo && gerador.PercPorAssunto.ContainsKey(q.AssuntoId));

    // Mesmo espírito de DisponiveisPorAssunto, agrupando pela Disciplina do
    // Assunto (Escopo Multidisciplinar/Curso).
    public int DisponiveisPorDisciplina(List<Questao> questoesDisponiveis, GeradorInput gerador, int disciplinaId, List<TipoQuestao> tiposPermitidos) =>
        PoolQualificado(questoesDisponiveis, gerador).Count(q => q.Assunto?.DisciplinaId == disciplinaId && tiposPermitidos.Contains(q.TipoQuestao));

    // Escolhe UMA substituta com mesmo Assunto/Dificuldade/Bloom (preferindo
    // mesmo Tipo, aceitando outro se não houver), nunca repetindo questão já na prova.
    public Questao? EscolherSubstituta(List<Questao> questoesDisponiveis, Questao atual, HashSet<int> idsNaProva, Dictionary<int, int> usosPorQuestao)
    {
        var candidatosBase = questoesDisponiveis
            .Where(q => !idsNaProva.Contains(q.Id))
            .Where(q => q.AssuntoId == atual.AssuntoId)
            .Where(q => q.Dificuldade == atual.Dificuldade)
            .Where(q => q.Bloom == atual.Bloom)
            .ToList();

        var candidatosMesmoTipo = candidatosBase.Where(q => q.TipoQuestao == atual.TipoQuestao).ToList();
        var candidatos = candidatosMesmoTipo.Count > 0 ? candidatosMesmoTipo : candidatosBase;

        return candidatos.Count == 0 ? null : SortearAleatorios(candidatos, 1, usosPorQuestao).First();
    }

    // --- Sorteio ---

    // Blueprint: aloca a Quantidade em cascata Assunto -> Dificuldade -> (opcional)
    // Bloom. Com poucas questões o arredondamento fica aproximado nas células mais finas.
    public SorteioResultado Sortear(
        List<Questao> questoesDisponiveis,
        List<Assunto> assuntosDisponiveis,
        SorteioParametros p,
        HashSet<int> idsAEvitar)
    {
        if (p.TiposPermitidos.Count == 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Selecione ao menos um tipo de questão.", Abortado = true };
        }

        if (p.PercPorAssunto.Count == 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Selecione ao menos um assunto.", Abortado = true };
        }

        if (p.Quantidade <= 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Informe a quantidade de questões.", Abortado = true };
        }

        var somaAssunto = p.PercPorAssunto.Values.Sum();
        if (somaAssunto != 100)
        {
            return new SorteioResultado
            {
                Sorteadas = new(),
                Aviso = $"Os percentuais de assunto precisam somar 100% (soma atual: {somaAssunto}%).",
                Abortado = true,
            };
        }

        var somaDificuldade = p.PercFacil + p.PercMedia + p.PercDificil;
        if (somaDificuldade != 100)
        {
            return new SorteioResultado
            {
                Sorteadas = new(),
                Aviso = $"Os percentuais de dificuldade precisam somar 100% (soma atual: {somaDificuldade}%).",
                Abortado = true,
            };
        }

        if (p.PercPorBloom.Count > 0)
        {
            var somaBloom = p.PercPorBloom.Values.Sum();
            if (somaBloom != 100)
            {
                return new SorteioResultado
                {
                    Sorteadas = new(),
                    Aviso = $"Os percentuais de Bloom precisam somar 100% (soma atual: {somaBloom}%).",
                    Abortado = true,
                };
            }
        }

        var qtdPorAssunto = DistribuirPorPercentual(p.PercPorAssunto, p.Quantidade);
        var percDificuldade = new Dictionary<Dificuldade, int>
        {
            [Dificuldade.Facil] = p.PercFacil,
            [Dificuldade.Media] = p.PercMedia,
            [Dificuldade.Dificil] = p.PercDificil,
        };

        var sorteadas = new List<Questao>();
        var idsJaConsiderados = new HashSet<int>(p.IdsJaSelecionados);
        var faltandoPorAssunto = new Dictionary<int, int>();

        foreach (var (assuntoId, qtdAssunto) in qtdPorAssunto)
        {
            if (qtdAssunto <= 0)
            {
                continue;
            }

            var poolAssunto = questoesDisponiveis
                .Where(q => q.AssuntoId == assuntoId)
                .Where(q => !idsAEvitar.Contains(q.Id))
                .Where(q => p.TiposPermitidos.Contains(q.TipoQuestao))
                .ToList();

            var qtdPorDificuldade = DistribuirPorPercentual(percDificuldade, qtdAssunto);

            foreach (var (nivel, qtdNivel) in qtdPorDificuldade)
            {
                if (qtdNivel <= 0)
                {
                    continue;
                }

                var poolDificuldade = poolAssunto.Where(q => q.Dificuldade == nivel).ToList();

                // Bloom vazio: sorteia a célula inteira de uma vez (aceita
                // até sem classificação). Com Bloom marcado, quebra de novo por nível.
                if (p.PercPorBloom.Count == 0)
                {
                    var elegiveis = poolDificuldade.Where(q => !idsJaConsiderados.Contains(q.Id)).ToList();
                    var escolhidas = SortearAleatorios(elegiveis, qtdNivel, p.UsosPorQuestao);
                    foreach (var q in escolhidas)
                    {
                        sorteadas.Add(q);
                        idsJaConsiderados.Add(q.Id);
                    }
                    if (escolhidas.Count < qtdNivel)
                    {
                        faltandoPorAssunto[assuntoId] = faltandoPorAssunto.GetValueOrDefault(assuntoId) + (qtdNivel - escolhidas.Count);
                    }
                }
                else
                {
                    var qtdPorBloom = DistribuirPorPercentual(p.PercPorBloom, qtdNivel);
                    foreach (var (nivelBloom, qtdBloom) in qtdPorBloom)
                    {
                        if (qtdBloom <= 0)
                        {
                            continue;
                        }

                        var elegiveis = poolDificuldade
                            .Where(q => q.Bloom == nivelBloom)
                            .Where(q => !idsJaConsiderados.Contains(q.Id))
                            .ToList();
                        var escolhidas = SortearAleatorios(elegiveis, qtdBloom, p.UsosPorQuestao);
                        foreach (var q in escolhidas)
                        {
                            sorteadas.Add(q);
                            idsJaConsiderados.Add(q.Id);
                        }
                        if (escolhidas.Count < qtdBloom)
                        {
                            faltandoPorAssunto[assuntoId] = faltandoPorAssunto.GetValueOrDefault(assuntoId) + (qtdBloom - escolhidas.Count);
                        }
                    }
                }
            }
        }

        string? aviso = null;
        if (faltandoPorAssunto.Count > 0)
        {
            var nomePorAssunto = assuntosDisponiveis.ToDictionary(a => a.Id, a => a.Nome);
            var detalhes = faltandoPorAssunto
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{nomePorAssunto.GetValueOrDefault(kv.Key, $"assunto #{kv.Key}")}: faltaram {kv.Value}");
            aviso = "O blueprint não pôde ser cumprido à risca por falta de questões em algumas células — " + string.Join("; ", detalhes) + ".";
        }

        return new SorteioResultado
        {
            Sorteadas = sorteadas,
            Aviso = aviso,
            Abortado = false,
        };
    }

    // --- Distribuição por Disciplina (Escopo Multidisciplinar) ---

    // Mutuamente exclusivo com Sortear() — mesma cascata trocando Assunto por
    // Disciplina. Bloom fica de fora; Dificuldade é global, não por disciplina.
    public SorteioResultado SortearPorDisciplina(
        List<Questao> questoesDisponiveis,
        List<Disciplina> disciplinasDisponiveis,
        Dictionary<int, int> percPorDisciplina,
        List<TipoQuestao> tiposPermitidos,
        int quantidade,
        int percFacil,
        int percMedia,
        int percDificil,
        HashSet<int> idsJaSelecionados,
        Dictionary<int, int> usosPorQuestao,
        HashSet<int> idsAEvitar)
    {
        if (tiposPermitidos.Count == 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Selecione ao menos um tipo de questão.", Abortado = true };
        }

        if (percPorDisciplina.Count == 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Configure a distribuição por disciplina.", Abortado = true };
        }

        if (quantidade <= 0)
        {
            return new SorteioResultado { Sorteadas = new(), Aviso = "Informe a quantidade de questões.", Abortado = true };
        }

        var somaDisciplina = percPorDisciplina.Values.Sum();
        if (somaDisciplina != 100)
        {
            return new SorteioResultado
            {
                Sorteadas = new(),
                Aviso = $"Os percentuais de disciplina precisam somar 100% (soma atual: {somaDisciplina}%).",
                Abortado = true,
            };
        }

        var somaDificuldade = percFacil + percMedia + percDificil;
        if (somaDificuldade != 100)
        {
            return new SorteioResultado
            {
                Sorteadas = new(),
                Aviso = $"Os percentuais de dificuldade precisam somar 100% (soma atual: {somaDificuldade}%).",
                Abortado = true,
            };
        }

        var qtdPorDisciplina = DistribuirPorPercentual(percPorDisciplina, quantidade);
        var percDificuldade = new Dictionary<Dificuldade, int>
        {
            [Dificuldade.Facil] = percFacil,
            [Dificuldade.Media] = percMedia,
            [Dificuldade.Dificil] = percDificil,
        };

        var sorteadas = new List<Questao>();
        var idsJaConsiderados = new HashSet<int>(idsJaSelecionados);
        var faltandoPorDisciplina = new Dictionary<int, int>();

        foreach (var (disciplinaId, qtdDisciplina) in qtdPorDisciplina)
        {
            if (qtdDisciplina <= 0)
            {
                continue;
            }

            var poolDisciplina = questoesDisponiveis
                .Where(q => q.Assunto?.DisciplinaId == disciplinaId)
                .Where(q => !idsAEvitar.Contains(q.Id))
                .Where(q => tiposPermitidos.Contains(q.TipoQuestao))
                .ToList();

            var qtdPorDificuldade = DistribuirPorPercentual(percDificuldade, qtdDisciplina);

            foreach (var (nivel, qtdNivel) in qtdPorDificuldade)
            {
                if (qtdNivel <= 0)
                {
                    continue;
                }

                var elegiveis = poolDisciplina
                    .Where(q => q.Dificuldade == nivel)
                    .Where(q => !idsJaConsiderados.Contains(q.Id))
                    .ToList();

                var escolhidas = SortearAleatorios(elegiveis, qtdNivel, usosPorQuestao);
                foreach (var q in escolhidas)
                {
                    sorteadas.Add(q);
                    idsJaConsiderados.Add(q.Id);
                }
                if (escolhidas.Count < qtdNivel)
                {
                    faltandoPorDisciplina[disciplinaId] = faltandoPorDisciplina.GetValueOrDefault(disciplinaId) + (qtdNivel - escolhidas.Count);
                }
            }
        }

        string? aviso = null;
        if (faltandoPorDisciplina.Count > 0)
        {
            var nomePorDisciplina = disciplinasDisponiveis.ToDictionary(d => d.Id, d => d.Nome);
            var detalhes = faltandoPorDisciplina
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{nomePorDisciplina.GetValueOrDefault(kv.Key, $"disciplina #{kv.Key}")}: faltaram {kv.Value}");
            aviso = "A distribuição por disciplina não pôde ser cumprida à risca por falta de questões — " + string.Join("; ", detalhes) + ".";
        }

        return new SorteioResultado
        {
            Sorteadas = sorteadas,
            Aviso = aviso,
            Abortado = false,
        };
    }

    // Blueprint Curricular, mutuamente exclusivo a Sortear(): eixo primário é
    // o Item de Matriz. Overlap resolvido gulosamente, item mais raro primeiro.
    public ResultadoBlueprintCurricular SortearPorBlueprintCurricular(
        List<Questao> questoesDisponiveis,
        List<ItemMatrizReferencia> itensDisponiveis,
        Dictionary<int, int> percPorItem,
        List<TipoQuestao> tiposPermitidos,
        int quantidade,
        HashSet<int> idsJaSelecionados,
        Dictionary<int, int> usosPorQuestao,
        HashSet<int> idsAEvitar)
    {
        if (tiposPermitidos.Count == 0)
        {
            return new ResultadoBlueprintCurricular
            {
                Sorteadas = new(), Aviso = "Selecione ao menos um tipo de questão.", Abortado = true,
                QtdPlanejadaPorItem = new(), QtdObtidaPorItem = new(),
            };
        }

        if (percPorItem.Count == 0)
        {
            return new ResultadoBlueprintCurricular
            {
                Sorteadas = new(), Aviso = "Marque ao menos um item de matriz com percentual pra usar o Blueprint Curricular.", Abortado = true,
                QtdPlanejadaPorItem = new(), QtdObtidaPorItem = new(),
            };
        }

        if (quantidade <= 0)
        {
            return new ResultadoBlueprintCurricular
            {
                Sorteadas = new(), Aviso = "Informe a quantidade de questões.", Abortado = true,
                QtdPlanejadaPorItem = new(), QtdObtidaPorItem = new(),
            };
        }

        var somaPercentual = percPorItem.Values.Sum();
        if (somaPercentual != 100)
        {
            return new ResultadoBlueprintCurricular
            {
                Sorteadas = new(),
                Aviso = $"Os percentuais por item precisam somar 100% (soma atual: {somaPercentual}%).",
                Abortado = true,
                QtdPlanejadaPorItem = new(),
                QtdObtidaPorItem = new(),
            };
        }

        var qtdPlanejadaPorItem = DistribuirPorPercentual(percPorItem, quantidade);

        // Pool base: tipo permitido + não usado recentemente, sem o corte
        // por Assunto/Dificuldade/Bloom deste modo.
        var poolBase = questoesDisponiveis
            .Where(q => tiposPermitidos.Contains(q.TipoQuestao))
            .Where(q => !idsAEvitar.Contains(q.Id))
            .ToList();

        // Raridade contada sobre TODO o pool, sem descontar idsJaSelecionados (diferente da simulação/aderência).
        var (sorteadas, qtdObtidaPorItem, faltandoPorItem) = AlocarPorItemMaisRaroPrimeiro(
            poolBase,
            qtdPlanejadaPorItem,
            idsJaSelecionados,
            ordenarExcluindoJaAlocados: false,
            escolher: (elegiveis, qtd) => SortearAleatorios(elegiveis, qtd, usosPorQuestao));

        string? aviso = null;
        if (faltandoPorItem.Count > 0)
        {
            var codigoPorItem = itensDisponiveis.ToDictionary(i => i.Id, i => i.Codigo);
            var detalhes = faltandoPorItem
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{codigoPorItem.GetValueOrDefault(kv.Key, $"item #{kv.Key}")}: faltaram {kv.Value}");
            aviso = "O blueprint curricular não pôde ser cumprido à risca — cada questão ocupa só uma posição, e não havia questões suficientes pra algum item — " + string.Join("; ", detalhes) + ".";
        }

        return new ResultadoBlueprintCurricular
        {
            Sorteadas = sorteadas,
            Aviso = aviso,
            Abortado = false,
            QtdPlanejadaPorItem = qtdPlanejadaPorItem,
            QtdObtidaPorItem = qtdObtidaPorItem,
        };
    }

    // Núcleo da alocação gulosa, reaproveitado pelo sorteio real e simulações.
    public static (List<Questao> Selecionadas, Dictionary<int, int> ObtidoPorItem, Dictionary<int, int> FaltandoPorItem) AlocarPorItemMaisRaroPrimeiro(
        List<Questao> pool,
        Dictionary<int, int> qtdPlanejadaPorItem,
        HashSet<int> idsJaAlocados,
        bool ordenarExcluindoJaAlocados,
        Func<List<Questao>, int, List<Questao>> escolher)
    {
        var alocadas = new HashSet<int>(idsJaAlocados);
        var selecionadas = new List<Questao>();
        var obtidoPorItem = new Dictionary<int, int>();
        var faltandoPorItem = new Dictionary<int, int>();

        var ordemItens = qtdPlanejadaPorItem
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => ordenarExcluindoJaAlocados
                ? pool.Count(q => !alocadas.Contains(q.Id) && q.ItensMatriz.Any(i => i.Id == kv.Key))
                : pool.Count(q => q.ItensMatriz.Any(i => i.Id == kv.Key)))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var itemId in ordemItens)
        {
            var qtdPedida = qtdPlanejadaPorItem[itemId];

            var elegiveis = pool
                .Where(q => !alocadas.Contains(q.Id))
                .Where(q => q.ItensMatriz.Any(i => i.Id == itemId))
                .ToList();

            var escolhidas = escolher(elegiveis, qtdPedida);

            foreach (var q in escolhidas)
            {
                selecionadas.Add(q);
                alocadas.Add(q.Id);
            }

            obtidoPorItem[itemId] = escolhidas.Count;

            if (escolhidas.Count < qtdPedida)
            {
                faltandoPorItem[itemId] = qtdPedida - escolhidas.Count;
            }
        }

        return (selecionadas, obtidoPorItem, faltandoPorItem);
    }

    // Maior resto (quota de Hare): cada categoria recebe o piso da cota
    // exata, e o resto vai pras maiores partes fracionárias descartadas.
    public static Dictionary<TChave, int> DistribuirPorPercentual<TChave>(IReadOnlyDictionary<TChave, int> percentuais, int quantidadeTotal)
        where TChave : notnull
    {
        var exatos = percentuais.ToDictionary(kv => kv.Key, kv => quantidadeTotal * kv.Value / 100.0);
        var resultado = exatos.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
        var faltam = quantidadeTotal - resultado.Values.Sum();

        foreach (var chave in exatos
            .OrderByDescending(kv => kv.Value - Math.Floor(kv.Value))
            .Select(kv => kv.Key)
            .Take(faltam))
        {
            resultado[chave]++;
        }

        return resultado;
    }

    // Sorteia sem repetição, preferindo quem foi menos usada, via
    // Efraimidis-Spirakis (chave aleatória u^(1/peso), pega as maiores). O(n log n).
    public static List<Questao> SortearAleatorios(List<Questao> lista, int quantidade, Dictionary<int, int> usosPorQuestao)
    {
        var k = Math.Min(Math.Max(quantidade, 0), lista.Count);
        if (k == 0)
        {
            return new List<Questao>();
        }

        return lista
            .Select(q =>
            {
                var peso = 1.0 / (usosPorQuestao.GetValueOrDefault(q.Id) + 1);
                var chave = Math.Pow(Random.Shared.NextDouble(), 1.0 / peso);
                return (Questao: q, Chave: chave);
            })
            .OrderByDescending(x => x.Chave)
            .Take(k)
            .Select(x => x.Questao)
            .ToList();
    }
}
