using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Consulta EF + regra de negócio de Provas (CRUD, candidatas pro gerador, validação
// de Escopo). Motor de sorteio vive em GeradorProvaService; exportação em Program.cs.
public class ProvaService(
    ApplicationDbContext db,
    QuestaoQueryService questaoQueryService,
    GeradorProvaService geradorProvaService,
    MatrizReferenciaService matrizReferenciaService)
{
    // --- Leitura ---

    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    // Cursos/Turmas do cabeçalho vêm de CursoService — ProvaForm injeta
    // direto, mesmo padrão de DisciplinaService.

    public async Task<PaginaResultado<Prova>> ListarAsync(ProvaFiltro filtro, string? meuId, int pagina, int tamanhoPagina)
    {
        var query = db.Provas
            .Include(p => p.Disciplina)
            .Include(p => p.Turma)
            .Include(p => p.ProvaQuestoes)
            .Include(p => p.ProvaDisciplinas)
                .ThenInclude(pd => pd.Disciplina)
            // AsSplitQuery: duas coleções (ProvaQuestoes/ProvaDisciplinas) no
            // mesmo Include geram produto cartesiano sem isso.
            .AsSplitQuery()
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

    // Loader leve (sem Include) — só confirma posse e mostra o título.
    public Task<Prova?> ObterPorIdAsync(int id) => db.Provas.FindAsync(id).AsTask();

    // Loader único pra edição/duplicação, já com as questões e o Assunto de cada uma.
    public Task<Prova?> CarregarComQuestoesAsync(int id) =>
        db.Provas
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.Assunto)
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.ItensMatriz)
            // Também carrega Escopo (Disciplinas/Matriz) pra reconstruir o
            // ProvaInput na edição — provas antigas ficam com coleção vazia.
            .Include(p => p.ProvaDisciplinas)
                .ThenInclude(pd => pd.Disciplina)
            .Include(p => p.MatrizReferencia)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);

    // Assuntos da disciplina + questões ativas/visíveis dentro deles — base
    // de candidatas pra seleção manual e pro sorteio automático.
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
            .AsNoTracking()
            .Include(q => q.Assunto)
            .Include(q => q.Tags)
            .Include(q => q.Imagens)
            .Include(q => q.ItensMatriz)
            .AsSplitQuery()
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa && assuntoIds.Contains(q.AssuntoId))
            .OrderByDescending(q => q.CriadoEm)
            .ToListAsync();

        // Coleção específica do tipo (Alternativas/Pares/Lacunas) não tem Include
        // comum — carregada em lote por subtipo, tudo AsNoTracking (pool é só leitura).
        var idsMultiplaEscolha = questoes.OfType<QuestaoMultiplaEscolha>().Select(q => q.Id).ToList();
        if (idsMultiplaEscolha.Count > 0)
        {
            var alternativasPorQuestao = (await db.AlternativasQuestao
                .AsNoTracking()
                .Where(a => idsMultiplaEscolha.Contains(a.QuestaoMultiplaEscolhaId))
                .ToListAsync())
                .ToLookup(a => a.QuestaoMultiplaEscolhaId);

            foreach (var q in questoes.OfType<QuestaoMultiplaEscolha>())
            {
                q.Alternativas = alternativasPorQuestao[q.Id].ToList();
            }
        }

        var idsAssociacao = questoes.OfType<QuestaoAssociacao>().Select(q => q.Id).ToList();
        if (idsAssociacao.Count > 0)
        {
            var paresPorQuestao = (await db.ParesAssociacao
                .AsNoTracking()
                .Where(p => idsAssociacao.Contains(p.QuestaoAssociacaoId))
                .ToListAsync())
                .ToLookup(p => p.QuestaoAssociacaoId);

            foreach (var q in questoes.OfType<QuestaoAssociacao>())
            {
                q.Pares = paresPorQuestao[q.Id].ToList();
            }
        }

        var idsLacunas = questoes.OfType<QuestaoLacunas>().Select(q => q.Id).ToList();
        if (idsLacunas.Count > 0)
        {
            var respostasPorQuestao = (await db.LacunasRespostas
                .AsNoTracking()
                .Where(l => idsLacunas.Contains(l.QuestaoLacunasId))
                .ToListAsync())
                .ToLookup(l => l.QuestaoLacunasId);

            foreach (var q in questoes.OfType<QuestaoLacunas>())
            {
                q.Lacunas = respostasPorQuestao[q.Id].ToList();
            }
        }

        return (assuntos, questoes);
    }

    // Ids de questões usadas recentemente pelo professor (escopo é sempre
    // "minhas provas") — carrega em memória e decide o corte em C#.
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

        // Por quantidade: pega as N provas mais recentes e evita toda
        // questão usada em qualquer uma delas.
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

    // Escopo (Disciplina/Multidisciplinar/Curso): EscopoProvaConfig.DoProvaInput
    // traduz ProvaInput -> config, reaproveitada aqui e por ProvaForm.razor.

    // Validação de CONTEÚDO depois da de FORMA: confirma contra o banco que
    // disciplina(s)/matriz pertencem ao curso — nunca confia só no que o POST aceitou.
    private async Task ValidarEscopoAsync(EscopoProvaConfig escopo)
    {
        var erroForma = escopo.Validar();
        if (erroForma is not null)
        {
            throw new OperacaoInvalidaException(erroForma);
        }

        if (escopo.Tipo == TipoEscopoProva.Multidisciplinar)
        {
            if (escopo.CursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione o curso desta prova multidisciplinar.");
            }

            var cursoId = escopo.CursoId.Value;
            var disciplinasDoCurso = await db.Turmas
                .Where(t => t.CursoId == cursoId && escopo.DisciplinaIds.Contains(t.DisciplinaId))
                .Select(t => t.DisciplinaId)
                .Distinct()
                .ToListAsync();

            if (escopo.DisciplinaIds.Except(disciplinasDoCurso).Any())
            {
                throw new OperacaoInvalidaException(
                    "Uma ou mais disciplinas selecionadas não têm turma cadastrada neste curso — confirme se pertencem a ele.");
            }
        }

        if (escopo.MatrizReferenciaId is int matrizId)
        {
            if (escopo.CursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione o curso antes de escolher a Matriz de Referência.");
            }

            var vinculaveis = await matrizReferenciaService.ListarVinculaveisPorCursoAsync(escopo.CursoId.Value);
            if (!vinculaveis.Any(m => m.Id == matrizId))
            {
                throw new OperacaoInvalidaException("A Matriz de Referência selecionada não pertence a este curso.");
            }
        }
    }

    // Última barreira de IDOR: reconsulta o banco e rejeita a operação
    // INTEIRA se alguma questão selecionada não pertencer ao escopo final.
    private async Task ValidarQuestoesNoEscopoAsync(
        EscopoProvaConfig escopo, List<QuestaoSelecionada> selecionadas, string? meuId, int? minhaInstituicaoId)
    {
        var ids = selecionadas.Select(s => s.QuestaoId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var validos = await questaoQueryService.ValidarQuestaoIdsNoEscopoAsync(
            escopo.ParaEscopoQuestoes(), ids, meuId, minhaInstituicaoId);

        if (validos.Count != ids.Count)
        {
            throw new OperacaoInvalidaException(
                "Uma ou mais questões selecionadas não pertencem ao escopo desta prova (verifique curso, disciplina(s) e matriz).");
        }
    }

    // Prévia pra avisar, ANTES de salvar, quantas questões já escolhidas na
    // tela ficariam fora do escopo novo (mesma regra de ValidarQuestoesNoEscopoAsync).
    public async Task<List<int>> PreverQuestoesForaDoNovoEscopoAsync(
        ProvaInput modelo, List<QuestaoSelecionada> selecionadas, string? meuId, int? minhaInstituicaoId)
    {
        var ids = selecionadas.Select(s => s.QuestaoId).Distinct().ToList();
        var escopo = EscopoProvaConfig.DoProvaInput(modelo);
        if (ids.Count == 0 || escopo.Validar() is not null)
        {
            return new List<int>();
        }

        var validos = await questaoQueryService.ValidarQuestaoIdsNoEscopoAsync(escopo.ParaEscopoQuestoes(), ids, meuId, minhaInstituicaoId);
        return ids.Except(validos).ToList();
    }

    // --- Geração automática (facade única) ---

    // Ponto de entrada único (GerarAsync/DiagnosticarAsync) — tudo vem de um
    // GeradorInput só; orquestra consulta + motor (GeradorProvaService) + consolidação.
    public async Task<GeracaoResultado> GerarAsync(
        GeradorInput gerador, string? meuId, int? minhaInstituicaoId, HashSet<int> idsJaSelecionados)
    {
        var (assuntos, candidatas) = await ObterCandidatasAsync(gerador.Escopo, meuId, minhaInstituicaoId);
        var itensCurricular = await ObterItensCurricularAsync(gerador.Escopo);
        var disciplinas = await ObterDisciplinasAsync(gerador.Escopo);

        var idsAEvitar = gerador.EvitarRecentes
            ? await ObterIdsRecentesAsync(meuId, gerador.CriterioRecentes, gerador.ValorCriterioRecentes)
            : new HashSet<int>();

        var usosPorQuestao = await ContarUsosAsync(candidatas.Select(q => q.Id).ToList());
        var pool = geradorProvaService.PoolQualificado(candidatas, gerador);

        SorteioResultado resultado;
        var aderenciaCurricular = new List<LinhaBlueprintCurricular>();

        if (gerador.ModoAlinhamentoCurricular == ModoAlinhamentoCurricular.BlueprintCurricular)
        {
            // Blueprint Curricular tem prioridade sobre distribuição por disciplina
            // (mutuamente exclusivos): o eixo primário vira o Item de Matriz.
            var resultadoBlueprint = geradorProvaService.SortearPorBlueprintCurricular(
                pool, itensCurricular, gerador.PercPorItem, gerador.TiposPermitidos(), gerador.Quantidade,
                idsJaSelecionados, usosPorQuestao, idsAEvitar);

            resultado = new SorteioResultado
            {
                Sorteadas = resultadoBlueprint.Sorteadas,
                Aviso = resultadoBlueprint.Aviso,
                Abortado = resultadoBlueprint.Abortado,
            };

            aderenciaCurricular = resultadoBlueprint.QtdPlanejadaPorItem
                .Where(kv => kv.Value > 0)
                .Select(kv => new { ItemId = kv.Key, Item = itensCurricular.FirstOrDefault(i => i.Id == kv.Key), Planejada = kv.Value })
                .Where(x => x.Item is not null)
                .Select(x => new LinhaBlueprintCurricular
                {
                    Item = x.Item!,
                    PercentualPlanejado = gerador.PercPorItem.GetValueOrDefault(x.ItemId),
                    QuantidadePlanejada = x.Planejada,
                    QuantidadeObtida = resultadoBlueprint.QtdObtidaPorItem.GetValueOrDefault(x.ItemId),
                })
                .OrderBy(l => l.Item.Codigo)
                .ToList();
        }
        else if (gerador.Escopo.Tipo == TipoEscopoProva.Multidisciplinar && gerador.DistribuicaoDisciplinas.Count > 0)
        {
            resultado = geradorProvaService.SortearPorDisciplina(
                pool, disciplinas, gerador.DistribuicaoDisciplinas, gerador.TiposPermitidos(), gerador.Quantidade,
                gerador.PercFacil, gerador.PercMedia, gerador.PercDificil, idsJaSelecionados, usosPorQuestao, idsAEvitar);
        }
        else if (gerador.DefinirQuantidadePorTipo && gerador.QuantidadePorTipo.Count > 0)
        {
            var sorteadasTotais = new List<Questao>();
            var idsAcumulados = new HashSet<int>(idsJaSelecionados);
            var avisos = new List<string>();
            var abortado = false;

            foreach (var (tipo, qtdTipo) in gerador.QuantidadePorTipo)
            {
                if (qtdTipo <= 0)
                {
                    continue;
                }

                var resultadoTipo = geradorProvaService.Sortear(pool, assuntos, new SorteioParametros
                {
                    PercPorAssunto = gerador.PercPorAssunto,
                    TiposPermitidos = new List<TipoQuestao> { tipo },
                    PercPorBloom = gerador.PercPorBloom,
                    Quantidade = qtdTipo,
                    PercFacil = gerador.PercFacil,
                    PercMedia = gerador.PercMedia,
                    PercDificil = gerador.PercDificil,
                    IdsJaSelecionados = idsAcumulados,
                    UsosPorQuestao = usosPorQuestao,
                }, idsAEvitar);

                if (resultadoTipo.Abortado)
                {
                    abortado = true;
                    avisos.Add($"{tipo.Rotulo()}: {resultadoTipo.Aviso}");
                    continue;
                }

                foreach (var q in resultadoTipo.Sorteadas)
                {
                    sorteadasTotais.Add(q);
                    idsAcumulados.Add(q.Id);
                }
                if (resultadoTipo.Aviso is not null)
                {
                    avisos.Add(resultadoTipo.Aviso);
                }
            }

            resultado = new SorteioResultado
            {
                Sorteadas = sorteadasTotais,
                Aviso = avisos.Count == 0 ? null : string.Join(" ", avisos),
                Abortado = abortado,
            };
        }
        else
        {
            resultado = geradorProvaService.Sortear(pool, assuntos, new SorteioParametros
            {
                PercPorAssunto = gerador.PercPorAssunto,
                TiposPermitidos = gerador.TiposPermitidos(),
                PercPorBloom = gerador.PercPorBloom,
                Quantidade = gerador.Quantidade,
                PercFacil = gerador.PercFacil,
                PercMedia = gerador.PercMedia,
                PercDificil = gerador.PercDificil,
                IdsJaSelecionados = idsJaSelecionados,
                UsosPorQuestao = usosPorQuestao,
            }, idsAEvitar);
        }

        return MontarGeracaoResultado(resultado, gerador, assuntos, disciplinas, aderenciaCurricular);
    }

    // Só diagnostica (nunca sorteia): "Solicitado × Disponível" por
    // Disciplina e por Item de Matriz, mais o total de distintas elegíveis.
    public async Task<GeracaoDiagnostico> DiagnosticarAsync(GeradorInput gerador, string? meuId, int? minhaInstituicaoId)
    {
        var (_, candidatas) = await ObterCandidatasAsync(gerador.Escopo, meuId, minhaInstituicaoId);
        var itensCurricular = await ObterItensCurricularAsync(gerador.Escopo);
        var disciplinas = await ObterDisciplinasAsync(gerador.Escopo);
        var tiposPermitidos = gerador.TiposPermitidos();

        var porDisciplina = new List<LinhaDiagnosticoDisciplina>();
        var alertas = new List<string>();

        if (gerador.Escopo.Tipo == TipoEscopoProva.Multidisciplinar && gerador.DistribuicaoDisciplinas.Count > 0)
        {
            var qtdPorDisciplina = GeradorProvaService.DistribuirPorPercentual(gerador.DistribuicaoDisciplinas, gerador.Quantidade);
            foreach (var (disciplinaId, solicitado) in qtdPorDisciplina)
            {
                var nome = disciplinas.FirstOrDefault(d => d.Id == disciplinaId)?.Nome ?? $"disciplina #{disciplinaId}";
                var disponivel = geradorProvaService.DisponiveisPorDisciplina(candidatas, gerador, disciplinaId, tiposPermitidos);
                porDisciplina.Add(new LinhaDiagnosticoDisciplina { DisciplinaId = disciplinaId, Nome = nome, Solicitado = solicitado, Disponivel = disponivel });
                if (disponivel < solicitado)
                {
                    alertas.Add($"{nome} solicita {solicitado} questão(ões), mas apenas {disponivel} disponíve(is) atendem aos demais filtros aplicados.");
                }
            }
        }

        var porItem = new List<LinhaDiagnosticoItem>();
        if (gerador.ModoAlinhamentoCurricular == ModoAlinhamentoCurricular.BlueprintCurricular && gerador.PercPorItem.Count > 0)
        {
            var qtdPorItem = GeradorProvaService.DistribuirPorPercentual(gerador.PercPorItem, gerador.Quantidade);
            foreach (var (itemId, solicitado) in qtdPorItem)
            {
                var item = itensCurricular.FirstOrDefault(i => i.Id == itemId);
                if (item is null)
                {
                    continue;
                }

                var disponivel = geradorProvaService.DisponiveisPorItem(candidatas, gerador, itemId, tiposPermitidos);
                porItem.Add(new LinhaDiagnosticoItem { Item = item, Solicitado = solicitado, Disponivel = disponivel });
                if (disponivel < solicitado)
                {
                    alertas.Add($"{item.Codigo} solicita {solicitado} questão(ões), mas apenas {disponivel} questões distintas atendem aos demais filtros aplicados.");
                }
            }
        }

        var idsItens = gerador.ModoAlinhamentoCurricular == ModoAlinhamentoCurricular.FiltroCurricular
            ? gerador.ItemMatrizIdsDesejados
            : new HashSet<int>(itensCurricular.Select(i => i.Id));

        var totalDistintas = geradorProvaService.DisponiveisDistintasParaConjunto(candidatas, gerador, idsItens, tiposPermitidos);

        return new GeracaoDiagnostico
        {
            TotalDistintasElegiveis = totalDistintas,
            PorDisciplina = porDisciplina,
            PorItemMatriz = porItem,
            Alertas = alertas,
        };
    }

    // Consolida o SorteioResultado do motor em GeracaoResultado: Aderência
    // (Assunto/Dificuldade/Bloom OU Curricular, nunca as duas) e Distribuição pela contagem real.
    private static GeracaoResultado MontarGeracaoResultado(
        SorteioResultado resultado, GeradorInput gerador, List<Assunto> assuntos, List<Disciplina> disciplinas,
        List<LinhaBlueprintCurricular> aderenciaCurricular)
    {
        var selecionadas = resultado.Sorteadas;
        var aderenciaAssuntoDificuldadeBloom = new List<LinhaAderencia>();

        if (selecionadas.Count > 0 && gerador.ModoAlinhamentoCurricular != ModoAlinhamentoCurricular.BlueprintCurricular)
        {
            var total = selecionadas.Count;
            var somaDificuldade = gerador.PercFacil + gerador.PercMedia + gerador.PercDificil;
            if (somaDificuldade == 100)
            {
                foreach (var (nivel, planejado) in new[]
                {
                    (Dificuldade.Facil, gerador.PercFacil),
                    (Dificuldade.Media, gerador.PercMedia),
                    (Dificuldade.Dificil, gerador.PercDificil),
                })
                {
                    var qtd = selecionadas.Count(q => q.Dificuldade == nivel);
                    aderenciaAssuntoDificuldadeBloom.Add(new LinhaAderencia
                    {
                        Rotulo = nivel.Rotulo(),
                        PlanejadoPercentual = planejado,
                        ObtidoPercentual = (int)Math.Round(100.0 * qtd / total),
                        ObtidoQuantidade = qtd,
                    });
                }
            }

            var somaAssuntos = gerador.PercPorAssunto.Values.Sum();
            if (somaAssuntos == 100)
            {
                foreach (var (assuntoId, planejado) in gerador.PercPorAssunto)
                {
                    var nome = assuntos.FirstOrDefault(a => a.Id == assuntoId)?.Nome ?? $"assunto #{assuntoId}";
                    var qtd = selecionadas.Count(q => q.AssuntoId == assuntoId);
                    aderenciaAssuntoDificuldadeBloom.Add(new LinhaAderencia
                    {
                        Rotulo = nome,
                        PlanejadoPercentual = planejado,
                        ObtidoPercentual = (int)Math.Round(100.0 * qtd / total),
                        ObtidoQuantidade = qtd,
                    });
                }
            }
        }

        // Contagem real por Disciplina — cada questão conta uma vez só, na
        // disciplina do seu Assunto (diferente de cobertura curricular).
        var distribuicaoPorDisciplina = selecionadas
            .Where(q => q.Assunto is not null)
            .GroupBy(q => q.Assunto!.DisciplinaId)
            .ToDictionary(g => g.Key, g => g.Count());

        var aderenciaDisciplina = new List<LinhaAderencia>();
        if (gerador.DistribuicaoDisciplinas.Count > 0 && selecionadas.Count > 0)
        {
            var total = selecionadas.Count;
            foreach (var (disciplinaId, planejado) in gerador.DistribuicaoDisciplinas)
            {
                var nome = disciplinas.FirstOrDefault(d => d.Id == disciplinaId)?.Nome ?? $"disciplina #{disciplinaId}";
                var qtd = distribuicaoPorDisciplina.GetValueOrDefault(disciplinaId);
                aderenciaDisciplina.Add(new LinhaAderencia
                {
                    Rotulo = nome,
                    PlanejadoPercentual = planejado,
                    ObtidoPercentual = (int)Math.Round(100.0 * qtd / total),
                    ObtidoQuantidade = qtd,
                });
            }
        }

        return new GeracaoResultado
        {
            Questoes = selecionadas,
            Aviso = resultado.Aviso,
            Abortado = resultado.Abortado,
            AderenciaAssuntoDificuldadeBloom = aderenciaAssuntoDificuldadeBloom,
            AderenciaCurricular = aderenciaCurricular,
            DistribuicaoPorDisciplina = distribuicaoPorDisciplina,
            AderenciaDisciplina = aderenciaDisciplina,
        };
    }

    // Wrappers finos que só traduzem EscopoProvaConfig pro formato de cada
    // consulta, sem duplicar a consulta em si.
    private async Task<(List<Assunto> Assuntos, List<Questao> Questoes)> ObterCandidatasAsync(
        EscopoProvaConfig escopo, string? meuId, int? minhaInstituicaoId) =>
        await questaoQueryService.ObterCandidatasAsync(escopo.ParaEscopoQuestoes(), meuId, minhaInstituicaoId);

    private async Task<List<ItemMatrizReferencia>> ObterItensCurricularAsync(EscopoProvaConfig escopo) =>
        escopo.MatrizReferenciaId is int matrizId
            ? await matrizReferenciaService.ListarItensAsync(matrizId)
            : new List<ItemMatrizReferencia>();

    private async Task<List<Disciplina>> ObterDisciplinasAsync(EscopoProvaConfig escopo) =>
        escopo.DisciplinaIds.Count == 0
            ? new List<Disciplina>()
            : await db.Disciplinas.AsNoTracking().Where(d => escopo.DisciplinaIds.Contains(d.Id)).ToListAsync();

    // --- Mutação ---

    public async Task<Prova> CriarAsync(
        ProvaInput modelo, List<QuestaoSelecionada> selecionadas, string? criadoPorId, int? minhaInstituicaoId)
    {
        if (selecionadas.Count == 0)
        {
            throw new OperacaoInvalidaException("Adicione pelo menos uma questão.");
        }

        var escopo = EscopoProvaConfig.DoProvaInput(modelo);
        await ValidarEscopoAsync(escopo);
        await ValidarQuestoesNoEscopoAsync(escopo, selecionadas, criadoPorId, minhaInstituicaoId);

        var prova = new Prova
        {
            Titulo = modelo.Titulo,
            TipoEscopo = modelo.TipoEscopo,
            // DisciplinaId só é preenchido no modo Disciplina — nos demais
            // modos as disciplinas vivem em ProvaDisciplinas.
            DisciplinaId = modelo.TipoEscopo == TipoEscopoProva.Disciplina && modelo.DisciplinaId != 0
                ? modelo.DisciplinaId
                : null,
            MatrizReferenciaId = modelo.MatrizReferenciaId,
            CursoId = modelo.CursoId == 0 ? null : modelo.CursoId,
            TurmaId = modelo.TurmaId == 0 ? null : modelo.TurmaId,
            Tipo = modelo.Tipo,
            Ano = modelo.Ano,
            Semestre = modelo.Semestre,
            Bimestre = modelo.Bimestre,
            DataAplicacao = modelo.DataAplicacao,
            TempoEstimadoMinutos = modelo.TempoEstimadoMinutos,
            Observacoes = modelo.Observacoes,
            MostrarValorNoEnunciado = modelo.MostrarValorNoEnunciado,
            CriadoPorId = criadoPorId,
        };

        AdicionarDisciplinasMultidisciplinar(prova, modelo);
        AdicionarQuestoesSelecionadas(prova, selecionadas);

        db.Provas.Add(prova);
        await db.SaveChangesAsync();
        return prova;
    }

    public async Task AtualizarAsync(
        int id, ProvaInput modelo, List<QuestaoSelecionada> selecionadas, string? meuId, int? minhaInstituicaoId)
    {
        if (selecionadas.Count == 0)
        {
            throw new OperacaoInvalidaException("Adicione pelo menos uma questão.");
        }

        var escopo = EscopoProvaConfig.DoProvaInput(modelo);
        await ValidarEscopoAsync(escopo);
        await ValidarQuestoesNoEscopoAsync(escopo, selecionadas, meuId, minhaInstituicaoId);

        var prova = await db.Provas
            .Include(p => p.ProvaQuestoes)
            .Include(p => p.ProvaDisciplinas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new OperacaoInvalidaException("Prova não encontrada.");

        if (prova.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você não tem permissão para editar esta prova.");
        }

        prova.Titulo = modelo.Titulo;
        prova.TipoEscopo = modelo.TipoEscopo;
        prova.DisciplinaId = modelo.TipoEscopo == TipoEscopoProva.Disciplina && modelo.DisciplinaId != 0
            ? modelo.DisciplinaId
            : null;
        prova.MatrizReferenciaId = modelo.MatrizReferenciaId;
        prova.CursoId = modelo.CursoId == 0 ? null : modelo.CursoId;
        prova.TurmaId = modelo.TurmaId == 0 ? null : modelo.TurmaId;
        prova.Tipo = modelo.Tipo;
        prova.Ano = modelo.Ano;
        prova.Semestre = modelo.Semestre;
        prova.Bimestre = modelo.Bimestre;
        prova.DataAplicacao = modelo.DataAplicacao;
        prova.TempoEstimadoMinutos = modelo.TempoEstimadoMinutos;
        prova.Observacoes = modelo.Observacoes;
        prova.MostrarValorNoEnunciado = modelo.MostrarValorNoEnunciado;

        // Mesma estratégia de sempre: apaga e recria do zero com a seleção
        // atual da tela (agora também pras disciplinas do Multidisciplinar).
        db.ProvasDisciplinas.RemoveRange(prova.ProvaDisciplinas);
        prova.ProvaDisciplinas.Clear();
        AdicionarDisciplinasMultidisciplinar(prova, modelo);

        db.ProvasQuestoes.RemoveRange(prova.ProvaQuestoes);
        prova.ProvaQuestoes.Clear();
        AdicionarQuestoesSelecionadas(prova, selecionadas);

        await db.SaveChangesAsync();
    }

    // Loop de ProvaDisciplinas consolidado (CriarAsync/AtualizarAsync usavam
    // o mesmo código duplicado).
    private static void AdicionarDisciplinasMultidisciplinar(Prova prova, ProvaInput modelo)
    {
        if (modelo.TipoEscopo != TipoEscopoProva.Multidisciplinar)
        {
            return;
        }

        foreach (var disciplinaId in modelo.DisciplinaIds)
        {
            prova.ProvaDisciplinas.Add(new ProvaDisciplina
            {
                DisciplinaId = disciplinaId,
                PercentualPlanejado = modelo.DistribuicaoDisciplinas.TryGetValue(disciplinaId, out var perc) && perc > 0
                    ? perc
                    : null,
            });
        }
    }

    // Loop de ProvaQuestoes consolidado (mesma duplicação de antes).
    private static void AdicionarQuestoesSelecionadas(Prova prova, List<QuestaoSelecionada> selecionadas)
    {
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
            .Include(p => p.ProvaDisciplinas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new OperacaoInvalidaException("Prova não encontrada.");

        var novoTitulo = $"{completa.Titulo} (cópia)";
        var copia = new Prova
        {
            Titulo = novoTitulo,
            TipoEscopo = completa.TipoEscopo,
            DisciplinaId = completa.DisciplinaId,
            MatrizReferenciaId = completa.MatrizReferenciaId,
            CursoId = completa.CursoId,
            TurmaId = completa.TurmaId,
            Tipo = completa.Tipo,
            Ano = completa.Ano,
            Semestre = completa.Semestre,
            Bimestre = completa.Bimestre,
            TempoEstimadoMinutos = completa.TempoEstimadoMinutos,
            Observacoes = completa.Observacoes,
            MostrarValorNoEnunciado = completa.MostrarValorNoEnunciado,
            // DataAplicacao não é copiada de propósito — é específica da aplicação original.
            CriadoPorId = meuId,
        };

        // Copia também as disciplinas/percentuais planejados (Multidisciplinar).
        foreach (var pd in completa.ProvaDisciplinas)
        {
            copia.ProvaDisciplinas.Add(new ProvaDisciplina
            {
                DisciplinaId = pd.DisciplinaId,
                PercentualPlanejado = pd.PercentualPlanejado,
            });
        }

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

    // Contagem de uso por questão (quantas provas a incluíram) — só o
    // número, usado pra ponderar o sorteio, não pra exibir na tela.
    public async Task<Dictionary<int, int>> ContarUsosAsync(List<int> questaoIds)
    {
        return await db.ProvasQuestoes
            .Where(pq => questaoIds.Contains(pq.QuestaoId))
            .GroupBy(pq => pq.QuestaoId)
            .Select(g => new { QuestaoId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.QuestaoId, g => g.Quantidade);
    }
}
