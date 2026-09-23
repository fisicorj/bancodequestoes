using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Consultas de Questão reutilizáveis (pesquisar/filtrar/paginar, estatística de
// uso), só leitura, separado de QuestaoService (que muta) pra reuso por outras telas.
public class QuestaoQueryService(ApplicationDbContext db)
{
    public async Task<PaginaResultado<Questao>> ListarAsync(QuestaoFiltro filtro, string? meuId, int? minhaInstituicaoId, int pagina, int tamanhoPagina)
    {
        var query = db.Questoes
            .Include(q => q.Assunto)
                .ThenInclude(a => a!.Disciplina)
            .Include(q => q.Tags)
            .Include(q => q.Imagens)
            .Include(q => q.ItensMatriz)
                .ThenInclude(i => i.MatrizReferencia)
            .Include(q => q.AreasCurso)
            // Quatro coleções incluídas ao mesmo tempo — sem AsSplitQuery
            // vira produto cartesiano (EFCore.Query[20504]).
            .AsSplitQuery()
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId);

        if (!filtro.MostrarInativas)
        {
            query = query.Where(q => q.Ativa);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            query = query.Where(q => EF.Functions.ILike(q.Enunciado, $"%{filtro.Texto}%"));
        }

        if (filtro.AssuntoId != 0)
        {
            query = query.Where(q => q.AssuntoId == filtro.AssuntoId);
        }
        else if (filtro.DisciplinaId != 0)
        {
            query = query.Where(q => q.Assunto!.DisciplinaId == filtro.DisciplinaId);
        }

        if (filtro.Tipo is { } tipo)
        {
            query = query.Where(q => q.TipoQuestao == tipo);
        }

        if (filtro.Dificuldade is { } dificuldade)
        {
            query = query.Where(q => q.Dificuldade == dificuldade);
        }

        if (filtro.Visibilidade is { } visibilidade)
        {
            query = query.Where(q => q.Visibilidade == visibilidade);
        }

        if (filtro.Bloom is { } bloom)
        {
            query = query.Where(q => q.Bloom == bloom);
        }

        if (filtro.Origem is { } origem)
        {
            query = query.Where(q => q.Origem == origem);
        }

        if (filtro.Ano is { } ano)
        {
            query = query.Where(q => q.Ano == ano);
        }

        if (filtro.SecaoEnade is { } secaoEnade)
        {
            query = query.Where(q => q.SecaoEnade == secaoEnade);
        }

        // "E" entre tags selecionadas (precisa ter TODAS): Where encadeado por
        // tag vira uma subquery EXISTS por tag, dando semântica de interseção.
        foreach (var tagId in filtro.TagIds)
        {
            query = query.Where(q => q.Tags.Any(t => t.Id == tagId));
        }

        // filtro.CursoId não filtra Questao diretamente (Questao.CursoId foi
        // removido) — só escopa quais Matriz/Item aparecem nos <select> da tela.
        if (filtro.AreaCursoId != 0)
        {
            query = query.Where(q => q.AreasCurso.Any(a => a.Id == filtro.AreaCursoId));
        }

        if (filtro.MatrizReferenciaId != 0)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => i.MatrizReferenciaId == filtro.MatrizReferenciaId));
        }

        // Item de matriz é flat — filtro direto por Id (Any vira EXISTS no SQL).
        if (filtro.ItemMatrizId != 0)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => i.Id == filtro.ItemMatrizId));
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(q => q.CriadoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        // Coleção do subtipo carregada uma por uma (N+1 mantido de propósito):
        // reaproveitada por QuestaoService.AlternarAtivaAsync, que precisa delas RASTREADAS.
        foreach (var q in itens)
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

        return new PaginaResultado<Questao> { Itens = itens, Total = total };
    }

    // Uma consulta só serve pros dois (contagem e última utilização) — traz
    // tudo pra memória e agrupa em C# em vez de duas idas ao banco.
    public async Task<(Dictionary<int, int> Usos, Dictionary<int, UltimoUsoInfo> UltimoUso)> ObterUsoAsync(List<int> questaoIds)
    {
        var usos = await db.ProvasQuestoes
            .Where(pq => questaoIds.Contains(pq.QuestaoId))
            .Select(pq => new
            {
                pq.QuestaoId,
                Titulo = pq.Prova!.Titulo,
                pq.Prova.Tipo,
                pq.Prova.DataAplicacao,
                pq.Prova.CriadoEm,
            })
            .ToListAsync();

        var contagem = usos
            .GroupBy(u => u.QuestaoId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Sem Data de aplicação, cai pra CriadoEm — prova recém-criada não fica sempre por último.
        var ultimoUso = usos
            .GroupBy(u => u.QuestaoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(u => u.DataAplicacao?.ToDateTime(TimeOnly.MinValue) ?? u.CriadoEm)
                    .Select(u => new UltimoUsoInfo { Titulo = u.Titulo, Tipo = u.Tipo, Data = u.DataAplicacao })
                    .First());

        return (contagem, ultimoUso);
    }

    // Pool de candidatas escopo-aware (Disciplina/Multidisciplinar/Curso); Assuntos
    // são DERIVADOS das Questoes já filtradas, pois Disciplina não tem CursoId aqui.
    private Task<int?> ObterAreaCursoIdDoCursoAsync(int cursoId) =>
        db.Cursos.Where(c => c.Id == cursoId).Select(c => c.AreaCursoId).FirstOrDefaultAsync();

    public async Task<(List<Assunto> Assuntos, List<Questao> Questoes)> ObterCandidatasAsync(
        EscopoQuestoes escopo, string? meuId, int? minhaInstituicaoId)
    {
        if (escopo.DisciplinaIds.Count == 0 && escopo.CursoId is null or 0)
        {
            return (new List<Assunto>(), new List<Questao>());
        }

        var query = db.Questoes
            .AsNoTracking()
            .Include(q => q.Assunto)
            .Include(q => q.Tags)
            .Include(q => q.Imagens)
            .Include(q => q.ItensMatriz)
            // Ver AsSplitQuery em ListarAsync acima.
            .AsSplitQuery()
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa);

        // Disciplina(s) tem prioridade sobre Curso quando ambos vierem
        // preenchidos (documenta a ordem, caso chamado com escopo montado à mão).
        if (escopo.DisciplinaIds.Count > 0)
        {
            query = query.Where(q => escopo.DisciplinaIds.Contains(q.Assunto!.DisciplinaId));
        }
        else if (escopo.CursoId is int cursoId)
        {
            // QuestaoAreaCurso é a única fonte de verdade de aplicabilidade: traduz
            // "Curso institucional" pra "AreaCurso nacional"; sem AreaCursoId, pool vazio de propósito.
            var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId);
            query = areaCursoId is int aid
                ? query.Where(q => q.AreasCurso.Any(a => a.Id == aid))
                : query.Where(q => false);
        }

        if (escopo.MatrizReferenciaId is int matrizId)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => i.MatrizReferenciaId == matrizId));
        }

        if (escopo.ItemMatrizIds.Count > 0)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => escopo.ItemMatrizIds.Contains(i.Id)));
        }

        var questoes = await query.OrderByDescending(q => q.CriadoEm).ToListAsync();

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

        var assuntoIds = questoes.Select(q => q.AssuntoId).Distinct().ToList();
        var assuntos = assuntoIds.Count == 0
            ? new List<Assunto>()
            : await db.Assuntos.AsNoTracking().Where(a => assuntoIds.Contains(a.Id)).OrderBy(a => a.Nome).ToListAsync();

        return (assuntos, questoes);
    }

    // Validação leve de escopo (nunca confiar só no frontend/IDOR): só
    // ".Select(q => q.Id)", usada por ProvaService.ValidarQuestoesNoEscopoAsync antes de salvar.
    public async Task<HashSet<int>> ValidarQuestaoIdsNoEscopoAsync(
        EscopoQuestoes escopo, List<int> questaoIds, string? meuId, int? minhaInstituicaoId)
    {
        if (questaoIds.Count == 0 || (escopo.DisciplinaIds.Count == 0 && escopo.CursoId is null or 0))
        {
            return new HashSet<int>();
        }

        var query = db.Questoes
            .AsNoTracking()
            .Where(q => questaoIds.Contains(q.Id))
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa);

        if (escopo.DisciplinaIds.Count > 0)
        {
            query = query.Where(q => escopo.DisciplinaIds.Contains(q.Assunto!.DisciplinaId));
        }
        else if (escopo.CursoId is int cursoId)
        {
            // Mesma tradução Curso -> AreaCurso de ObterCandidatasAsync —
            // precisa ser idêntica, senão um id aceito lá seria rejeitado aqui, ou vice-versa.
            var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId);
            query = areaCursoId is int aid
                ? query.Where(q => q.AreasCurso.Any(a => a.Id == aid))
                : query.Where(q => false);
        }

        if (escopo.MatrizReferenciaId is int matrizId)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => i.MatrizReferenciaId == matrizId));
        }

        if (escopo.ItemMatrizIds.Count > 0)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => escopo.ItemMatrizIds.Contains(i.Id)));
        }

        return (await query.Select(q => q.Id).ToListAsync()).ToHashSet();
    }

    public async Task<UltimoUsoInfo?> ObterUltimoUsoAsync(int questaoId)
    {
        var usos = await db.ProvasQuestoes
            .Where(pq => pq.QuestaoId == questaoId)
            .Select(pq => new
            {
                Titulo = pq.Prova!.Titulo,
                pq.Prova.Tipo,
                pq.Prova.DataAplicacao,
                pq.Prova.CriadoEm,
            })
            .ToListAsync();

        return usos
            .OrderByDescending(u => u.DataAplicacao?.ToDateTime(TimeOnly.MinValue) ?? u.CriadoEm)
            .Select(u => new UltimoUsoInfo { Titulo = u.Titulo, Tipo = u.Tipo, Data = u.DataAplicacao })
            .FirstOrDefault();
    }
}
