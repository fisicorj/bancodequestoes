using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Concentra consulta EF + regra de negócio de Provas: listagem/filtros,
// carregamento pra edição, geração automática (sorteio por dificuldade,
// exclusão de questões usadas recentemente), criar/editar com validação,
// excluir, duplicar. ProvaForm.razor e ProvaList.razor ficam só com UI +
// binding — mesmo padrão do DisciplinaService/QuestaoService.
//
// A exportação (.docx/.pdf/variações) continua nos endpoints do Program.cs
// via ProvaExportLoader/ProvaDocxExporter/ProvaPdfExporter — isso é
// ExportacaoService, o próximo passo dessa refatoração, não este.
public class ProvaService(ApplicationDbContext db)
{
    // --- Leitura ---

    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    // Cursos/Turmas pro cabeçalho e seletor da prova são responsabilidade do
    // CursoService (ListarTodosAsync/ListarTodasTurmasAsync) — ProvaForm
    // injeta os dois Services direto, mesmo padrão já usado com
    // DisciplinaService pra lista de disciplinas.

    public async Task<PaginaResultado<Prova>> ListarAsync(ProvaFiltro filtro, string? meuId, int pagina, int tamanhoPagina)
    {
        var query = db.Provas
            .Include(p => p.Disciplina)
            .Include(p => p.Turma)
            .Include(p => p.ProvaQuestoes)
            .Where(p => p.CriadoPorId == meuId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            query = query.Where(p => EF.Functions.ILike(p.Titulo, $"%{filtro.Texto}%"));
        }

        if (filtro.DisciplinaId != 0)
        {
            query = query.Where(p => p.DisciplinaId == filtro.DisciplinaId);
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(p => p.CriadoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Prova> { Itens = itens, Total = total };
    }

    // Loader simples (sem Include) — usado por telas que só precisam confirmar
    // posse e mostrar o título (ex.: ProvaVariacoes.razor), sem o custo de
    // carregar as questões da prova inteira.
    public Task<Prova?> ObterPorIdAsync(int id) => db.Provas.FindAsync(id).AsTask();

    // Loader único pra edição e duplicação — traz as questões da prova com
    // Assunto (a tela de edição precisa mostrar o enunciado + assunto de
    // cada uma na lista "Questões da prova").
    public Task<Prova?> CarregarComQuestoesAsync(int id) =>
        db.Provas
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.Assunto)
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.ItensMatriz)
            .FirstOrDefaultAsync(p => p.Id == id);

    // Assuntos da disciplina + questões ativas e visíveis pro professor
    // dentro deles — a base de candidatas tanto pra seleção manual quanto
    // pro sorteio automático.
    public async Task<(List<Assunto> Assuntos, List<Questao> Questoes)> ObterAssuntosEQuestoesDisponiveisAsync(
        int disciplinaId, string? meuId, int? minhaInstituicaoId)
    {
        if (disciplinaId == 0)
        {
            return (new List<Assunto>(), new List<Questao>());
        }

        var assuntos = await db.Assuntos
            .Where(a => a.DisciplinaId == disciplinaId)
            .OrderBy(a => a.Nome)
            .ToListAsync();

        var assuntoIds = assuntos.Select(a => a.Id).ToList();
        var questoes = await db.Questoes
            .Include(q => q.Assunto)
            .Include(q => q.Tags)
            .Include(q => q.Imagens)
            .Include(q => q.ItensMatriz)
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa && assuntoIds.Contains(q.AssuntoId))
            .OrderByDescending(q => q.CriadoEm)
            .ToListAsync();

        // A coleção específica do tipo (Alternativas/Pares/Lacunas) não dá pra
        // trazer com um .Include comum aqui porque ela só existe na subclasse,
        // não em Questao (mesma técnica de QuestaoService.ListarAsync) — precisa
        // estar carregada tanto pro farol de qualidade (QualidadeQuestao,
        // usado no diagnóstico do banco e no filtro de qualidade mínima do
        // gerador) quanto pro modal "Visualizar questão" na montagem manual.
        foreach (var q in questoes)
        {
            switch (q)
            {
                case QuestaoMultiplaEscolha me:
                    await db.Entry(me).Collection(m => m.Alternativas).LoadAsync();
                    break;
                case QuestaoAssociacao assoc:
                    await db.Entry(assoc).Collection(a => a.Pares).LoadAsync();
                    break;
                case QuestaoLacunas lac:
                    await db.Entry(lac).Collection(l => l.Lacunas).LoadAsync();
                    break;
            }
        }

        return (assuntos, questoes);
    }

    // Ids de questões a EVITAR por já terem sido usadas recentemente pelo
    // professor — escopo é sempre "minhas provas" (provas são privadas), não
    // o banco inteiro. Carrega tudo em memória (o volume de provas de um
    // professor é pequeno) e decide o corte em C#, já que "DataAplicacao ??
    // CriadoEm" não é o tipo de comparação que o EF traduz bem pra SQL.
    public async Task<HashSet<int>> ObterIdsRecentesAsync(string? meuId, CriterioEvitarRecentes criterio, int valorCriterio)
    {
        var usos = await db.ProvasQuestoes
            .Where(pq => pq.Prova!.CriadoPorId == meuId)
            .Select(pq => new
            {
                pq.QuestaoId,
                pq.ProvaId,
                pq.Prova!.DataAplicacao,
                pq.Prova.CriadoEm,
            })
            .ToListAsync();

        var n = Math.Max(valorCriterio, 0);

        if (criterio == CriterioEvitarRecentes.PorMeses)
        {
            var limite = DateTime.UtcNow.AddMonths(-n);
            return usos
                .Where(u => (u.DataAplicacao?.ToDateTime(TimeOnly.MinValue) ?? u.CriadoEm) >= limite)
                .Select(u => u.QuestaoId)
                .ToHashSet();
        }

        // Por quantidade de provas: pega as N provas mais recentes (por data
        // de aplicação, ou data de criação quando ainda não tem data marcada)
        // e evita toda questão usada em qualquer uma delas.
        var provasRecentes = usos
            .Select(u => new { u.ProvaId, Data = u.DataAplicacao?.ToDateTime(TimeOnly.MinValue) ?? u.CriadoEm })
            .Distinct()
            .OrderByDescending(x => x.Data)
            .Take(n)
            .Select(x => x.ProvaId)
            .ToHashSet();

        return usos
            .Where(u => provasRecentes.Contains(u.ProvaId))
            .Select(u => u.QuestaoId)
            .ToHashSet();
    }

    // Blueprint de avaliação: aloca a Quantidade em cascata — primeiro por
    // Assunto (PercPorAssunto), depois dentro de cada Assunto por Dificuldade
    // (PercFacil/Media/Dificil), e opcionalmente dentro de cada célula por
    // Bloom (PercPorBloom) — função pura, sem acesso a banco: recebe as
    // candidatas já carregadas (questoesDisponiveis), a lista de assuntos só
    // pra dar nome às mensagens de aviso, e os ids a evitar já calculados
    // (ObterIdsRecentesAsync, quando aplicável).
    //
    // Com poucas questões pedidas e três eixos de percentual, o arredondamento
    // em cascata é inevitavelmente aproximado nas células mais finas (ex.: 10
    // questões espalhadas em 4 assuntos × 3 dificuldades × 3 níveis de Bloom
    // dá células de 0 ou 1) — isso é esperado, não um bug: o blueprint é uma
    // meta que o gerador se aproxima ao máximo, avisando quando não conseguiu
    // bater exatamente por falta de questão numa célula específica.
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

                // Bloom vazio = não filtra por isso (aceita até questão sem
                // classificação), então a célula inteira é sorteada de uma
                // vez. Bloom com algo marcado = quebra essa célula de novo,
                // por nível de Bloom — só questões JÁ classificadas num
                // desses níveis entram, uma sem Bloom fica de fora.
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

    // --- Blueprint Curricular (itens 2/3/4 da 2ª rodada de revisão) ---
    //
    // Modo ALTERNATIVO a Sortear() acima — não é "mais uma dimensão" dentro
    // da mesma cascata Assunto→Dificuldade→Bloom (o que exigiria um solver
    // combinatorial pra não deixar um eixo "roubar" questão de outro que se
    // sobrepõe). É um modo PARALELO e mutuamente exclusivo: quando o
    // gerador está em Blueprint Curricular, o eixo primário de alocação
    // passa a ser o Item de Matriz (Competência/Conteúdo/etc.), com cota
    // percentual própria (reaproveitando DistribuirPorPercentual, igual o
    // pedido exige — "não duplicar lógica de distribuição"); os percentuais
    // de Assunto/Dificuldade/Bloom ficam inativos nesse modo (a tela some
    // com essas tabelas — ver GeradorAutomatico.razor) — simplificação
    // deliberada e documentada no relatório final, não um requisito
    // esquecido.
    //
    // Overlap entre itens (item 4: C08 e CT20 podem ser as MESMAS questões)
    // é tratado com uma estratégia gulosa determinística: processa os itens
    // do MAIS RARO pro MENOS RARO (menor pool disponível primeiro) e cada
    // questão sorteada é IMEDIATAMENTE removida do pool de todo mundo (via
    // "alocadas") — cada questão ocupa NO MÁXIMO uma posição do blueprint
    // (item 3: "cada questão deve ocupar apenas uma posição da prova"),
    // mesmo que ela atenda vários itens ao mesmo tempo. Não é um solver
    // ótimo (podia, em tese, escolher pior que um bin-packing perfeito em
    // casos adversariais), mas é determinístico, simples de explicar e
    // "suficientemente robusto" — exatamente o que o item 4 pede ("não
    // precisa implementar um solver matemático extremamente complexo").
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

        // Pool base: tipo permitido + não usado recentemente — igual ao
        // Sortear "de verdade" acima, mas SEM o corte por Assunto/
        // Dificuldade/Bloom (simplificação documentada no cabeçalho deste
        // método).
        var poolBase = questoesDisponiveis
            .Where(q => tiposPermitidos.Contains(q.TipoQuestao))
            .Where(q => !idsAEvitar.Contains(q.Id))
            .ToList();

        var alocadas = new HashSet<int>(idsJaSelecionados);
        var sorteadas = new List<Questao>();
        var qtdObtidaPorItem = new Dictionary<int, int>();
        var faltandoPorItem = new Dictionary<int, int>();

        // Ordem gulosa determinística: item com MENOS questões disponíveis
        // primeiro (item 4/5 do pedido) — quem tem pouca sobra não fica
        // "roubado" por um item mais genérico processado antes dele.
        var ordemItens = qtdPlanejadaPorItem
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => poolBase.Count(q => q.ItensMatriz.Any(i => i.Id == kv.Key)))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var itemId in ordemItens)
        {
            var qtdPedida = qtdPlanejadaPorItem[itemId];

            var elegiveis = poolBase
                .Where(q => !alocadas.Contains(q.Id))
                .Where(q => q.ItensMatriz.Any(i => i.Id == itemId))
                .ToList();

            var escolhidas = SortearAleatorios(elegiveis, qtdPedida, usosPorQuestao);

            foreach (var q in escolhidas)
            {
                sorteadas.Add(q);
                alocadas.Add(q.Id);
            }

            qtdObtidaPorItem[itemId] = escolhidas.Count;

            if (escolhidas.Count < qtdPedida)
            {
                faltandoPorItem[itemId] = qtdPedida - escolhidas.Count;
            }
        }

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

    // Maior resto (quota de Hare): cada categoria recebe o "piso" da sua cota
    // exata, e o resto da quantidadeTotal (que sobra por causa do
    // arredondamento) é distribuído uma unidade por vez pras categorias com
    // maior parte fracionária descartada. É o jeito padrão de arredondar
    // percentuais pra inteiros garantindo que a soma bate exatamente com o
    // total, sem favorecer sistematicamente a primeira nem a última categoria
    // (ao contrário de "a última absorve o resto").
    // Público (não mais privado): ProvaForm.razor também usa isso agora, pra
    // mostrar ao vivo quantas questões cada célula do blueprint (dificuldade,
    // assunto) vai render antes mesmo de clicar em "Sortear" — mesma conta,
    // sem duplicar o algoritmo em dois lugares.
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

    // --- Mutação ---

    public async Task<Prova> CriarAsync(ProvaInput modelo, List<QuestaoSelecionada> selecionadas, string? criadoPorId)
    {
        ValidarModelo(modelo, selecionadas);

        var prova = new Prova
        {
            Titulo = modelo.Titulo,
            DisciplinaId = modelo.DisciplinaId,
            CursoId = modelo.CursoId == 0 ? null : modelo.CursoId,
            TurmaId = modelo.TurmaId == 0 ? null : modelo.TurmaId,
            Tipo = modelo.Tipo,
            Ano = modelo.Ano,
            Semestre = modelo.Semestre,
            Bimestre = modelo.Bimestre,
            DataAplicacao = modelo.DataAplicacao,
            TempoEstimadoMinutos = modelo.TempoEstimadoMinutos,
            Observacoes = modelo.Observacoes,
            CriadoPorId = criadoPorId,
        };

        var ordem = 0;
        foreach (var s in selecionadas.OrderBy(s => s.Ordem))
        {
            prova.ProvaQuestoes.Add(new ProvaQuestao
            {
                QuestaoId = s.QuestaoId,
                Ordem = ordem++,
                Valor = s.Valor,
            });
        }

        db.Provas.Add(prova);
        await db.SaveChangesAsync();
        return prova;
    }

    public async Task AtualizarAsync(int id, ProvaInput modelo, List<QuestaoSelecionada> selecionadas, string? meuId)
    {
        ValidarModelo(modelo, selecionadas);

        var prova = await db.Provas
            .Include(p => p.ProvaQuestoes)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new OperacaoInvalidaException("Prova não encontrada.");

        if (prova.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você não tem permissão para editar esta prova.");
        }

        prova.Titulo = modelo.Titulo;
        prova.DisciplinaId = modelo.DisciplinaId;
        prova.CursoId = modelo.CursoId == 0 ? null : modelo.CursoId;
        prova.TurmaId = modelo.TurmaId == 0 ? null : modelo.TurmaId;
        prova.Tipo = modelo.Tipo;
        prova.Ano = modelo.Ano;
        prova.Semestre = modelo.Semestre;
        prova.Bimestre = modelo.Bimestre;
        prova.DataAplicacao = modelo.DataAplicacao;
        prova.TempoEstimadoMinutos = modelo.TempoEstimadoMinutos;
        prova.Observacoes = modelo.Observacoes;

        // Mais simples que casar item a item: apaga o vínculo antigo e recria do
        // zero com a seleção atual da tela.
        db.ProvasQuestoes.RemoveRange(prova.ProvaQuestoes);
        prova.ProvaQuestoes.Clear();

        var ordem = 0;
        foreach (var s in selecionadas.OrderBy(s => s.Ordem))
        {
            prova.ProvaQuestoes.Add(new ProvaQuestao
            {
                QuestaoId = s.QuestaoId,
                Ordem = ordem++,
                Valor = s.Valor,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Prova p)
    {
        try
        {
            db.Provas.Remove(p);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException($"Não foi possível excluir \"{p.Titulo}\".");
        }
    }

    public async Task<Prova> DuplicarAsync(int id, string? meuId)
    {
        var completa = await db.Provas
            .Include(p => p.ProvaQuestoes)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new OperacaoInvalidaException("Prova não encontrada.");

        var novoTitulo = $"{completa.Titulo} (cópia)";
        var copia = new Prova
        {
            Titulo = novoTitulo,
            DisciplinaId = completa.DisciplinaId,
            CursoId = completa.CursoId,
            TurmaId = completa.TurmaId,
            Tipo = completa.Tipo,
            Ano = completa.Ano,
            Semestre = completa.Semestre,
            Bimestre = completa.Bimestre,
            TempoEstimadoMinutos = completa.TempoEstimadoMinutos,
            Observacoes = completa.Observacoes,
            // DataAplicacao NÃO é copiada de propósito: é específica da
            // aplicação original, a cópia ainda não tem uma data marcada.
            CriadoPorId = meuId,
        };

        foreach (var pq in completa.ProvaQuestoes)
        {
            copia.ProvaQuestoes.Add(new ProvaQuestao
            {
                QuestaoId = pq.QuestaoId,
                Ordem = pq.Ordem,
                Valor = pq.Valor,
            });
        }

        db.Provas.Add(copia);
        await db.SaveChangesAsync();
        return copia;
    }

    private static void ValidarModelo(ProvaInput modelo, List<QuestaoSelecionada> selecionadas)
    {
        if (modelo.DisciplinaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma disciplina.");
        }

        if (selecionadas.Count == 0)
        {
            throw new OperacaoInvalidaException("Adicione pelo menos uma questão.");
        }
    }

    // Sorteia até "quantidade" questões sem repetição, com preferência por
    // quem foi MENOS usada (controle de exposição) — nunca é uma exclusão
    // dura: se só sobrar questão muito usada pra fechar a cota, ela ainda é
    // sorteada, só fica em desvantagem quando há opção menos exposta.
    //
    // Usa o truque de Efraimidis-Spirakis pra weighted sampling without
    // replacement: cada item recebe uma "chave" aleatória u^(1/peso) (u
    // uniforme em (0,1)) e pegamos as "quantidade" MAIORES chaves. Peso maior
    // (usada menos vezes) empurra a chave pra cima com mais força; peso menor
    // (muito usada) deixa a chave quase sempre baixa, mas ainda com alguma
    // chance. É O(n log n) por causa da ordenação — voltamos a pagar esse
    // preço (era O(n) no Fisher-Yates parcial de antes) porque não dá pra
    // fazer seleção PONDERADA em O(n) com o mesmo truque de troca; pra um
    // banco de questões isso continua irrelevante na prática.
    // Público (não mais privado): ProvaForm.razor também usa isso agora pra
    // "🔄 Substituir" (item 14) — escolher UMA substituta entre as candidatas
    // do mesmo assunto/dificuldade/Bloom seguindo a mesma preferência por
    // quem foi menos exposta, em vez de duplicar a lógica de sorteio.
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

    // Contagem de uso por questão (quantas provas já a incluíram) — só o
    // número, sem os detalhes de "última utilização" que
    // QuestaoService.ObterUsoAsync also devolve, porque aqui é usado só pra
    // ponderar o sorteio (ver SortearAleatorios), não pra exibir na tela.
    public async Task<Dictionary<int, int>> ContarUsosAsync(List<int> questaoIds)
    {
        return await db.ProvasQuestoes
            .Where(pq => questaoIds.Contains(pq.QuestaoId))
            .GroupBy(pq => pq.QuestaoId)
            .Select(g => new { QuestaoId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.QuestaoId, g => g.Quantidade);
    }
}
