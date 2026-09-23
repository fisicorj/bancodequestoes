using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Wrapper fino sobre BancoQuestoes.Importacao: converte QuestaoImportada em
// Questao e salva. "$CATEGORY:" marca Item de Matriz; "$ASSUNTO:" permite lote multi-área por grupo.
public class ImportacaoService(ApplicationDbContext db, MatrizReferenciaService matrizService, DisciplinaService disciplinaService)
{
    public ResultadoImportacao Analisar(string texto, string formato) =>
        formato == "Gift" ? GiftParser.Parse(texto) : AikenParser.Parse(texto);

    // Anota CodigosNaoEncontrados na pré-visualização — a resolução de
    // verdade acontece de novo, independente, dentro de ImportarAsync.
    public async Task AnotarItensMatrizAsync(ResultadoImportacao resultado, int? cursoId)
    {
        if (cursoId is null or 0 || !resultado.Questoes.Any(q => q.CodigosItemMatriz.Count > 0))
        {
            return;
        }

        var (itens, _) = await ObterItensEAreaDoCursoAsync(cursoId.Value);
        var itensPorCodigo = itens.ToLookup(i => i.Codigo, StringComparer.OrdinalIgnoreCase);

        foreach (var q in resultado.Questoes)
        {
            q.CodigosNaoEncontrados = q.CodigosItemMatriz.Where(c => !itensPorCodigo.Contains(c)).ToList();
        }
    }

    // Une itens institucionais do Curso com os nacionais da Área que ele
    // representa; devolve a AreaCurso pra ImportarAsync vincular a questão.
    private async Task<(List<ItemMatrizReferencia> Itens, AreaCurso? Area)> ObterItensEAreaDoCursoAsync(int cursoId)
    {
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId);
        var itensInstitucionais = await matrizService.ListarItensVinculaveisPorCursoAsync(cursoId);

        if (curso?.AreaCursoId is not int areaCursoId)
        {
            return (itensInstitucionais, null);
        }

        var area = await db.AreasCurso.FirstOrDefaultAsync(a => a.Id == areaCursoId);
        var itensNacionais = await matrizService.ListarItensVinculaveisPorAreasCursoAsync(new[] { areaCursoId });
        return (itensInstitucionais.Concat(itensNacionais).DistinctBy(i => i.Id).ToList(), area);
    }

    // Agrupa questões com "$ASSUNTO:" e verifica se cada combinação já existe
    // (case-insensitive). Idempotente — chamar de novo reconstrói a lista.
    public async Task ResolverAssuntosAsync(ResultadoImportacao resultado)
    {
        var grupos = resultado.Questoes
            .Where(q => q.DisciplinaSugerida is not null && q.AssuntoSugerido is not null)
            .GroupBy(q => (Disciplina: q.DisciplinaSugerida!, Assunto: q.AssuntoSugerido!))
            .Select(g => new GrupoAssuntoImportacao
            {
                Disciplina = g.Key.Disciplina,
                Assunto = g.Key.Assunto,
                QuantidadeQuestoes = g.Count(),
                NomeDisciplinaFinal = g.Key.Disciplina,
                NomeAssuntoFinal = g.Key.Assunto,
            })
            .ToList();

        if (grupos.Count > 0)
        {
            // Mesma listagem usada pelos seletores de QuestaoForm/ProvaForm — carregada uma vez, fora do laço.
            var assuntosExistentes = await disciplinaService.ListarTodosAssuntosAsync();

            foreach (var grupo in grupos)
            {
                var existente = assuntosExistentes.FirstOrDefault(a =>
                    string.Equals(a.Disciplina?.Nome, grupo.Disciplina, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.Nome, grupo.Assunto, StringComparison.OrdinalIgnoreCase));

                if (existente is not null)
                {
                    grupo.DisciplinaIdEncontrada = existente.DisciplinaId;
                    grupo.AssuntoIdEncontrado = existente.Id;
                    grupo.AssuntoIdEscolhido = existente.Id;
                    grupo.CriarNovo = false; // achou — a tela pode oferecer trocar, mas o padrão é usar o que já existe
                }
                else
                {
                    grupo.DisciplinaIdEncontrada = assuntosExistentes
                        .Select(a => a.Disciplina)
                        .FirstOrDefault(d => d is not null && string.Equals(d.Nome, grupo.Disciplina, StringComparison.OrdinalIgnoreCase))
                        ?.Id;

                    // Se o texto do ASSUNTO bate com nome de Disciplina existente, nunca cria sozinho.
                    var disciplinaHomonima = assuntosExistentes
                        .Select(a => a.Disciplina)
                        .FirstOrDefault(d => d is not null && string.Equals(d.Nome, grupo.Assunto, StringComparison.OrdinalIgnoreCase));

                    if (disciplinaHomonima is not null)
                    {
                        grupo.DisciplinaHomonimaEncontradaId = disciplinaHomonima.Id;
                        grupo.DisciplinaHomonimaEncontradaNome = disciplinaHomonima.Nome;
                        grupo.CriarNovo = false;
                    }
                    else
                    {
                        grupo.CriarNovo = true; // não achou nada parecido — propõe criar, professor pode trocar na tela
                    }
                }

                // Pré-seleciona o filtro de Disciplina da tela com o que já foi
                // encontrado por nome — menos cliques quando a Disciplina bateu.
                grupo.DisciplinaIdFiltro = grupo.DisciplinaIdEncontrada ?? 0;
            }
        }

        resultado.GruposAssunto = grupos;
    }

    public async Task<int> ImportarAsync(ResultadoImportacao resultado, int assuntoId, Dificuldade dificuldade, int? cursoId, string? criadoPorId)
    {
        // assuntoId da tela só é obrigatório pras questões sem "$ASSUNTO:" próprio.
        var precisaDoAssuntoDaTela = resultado.Questoes.Any(q => q.Selecionada && q.AssuntoSugerido is null);
        if (precisaDoAssuntoDaTela && assuntoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione o assunto de destino (há questões sem \"$ASSUNTO:\" próprio).");
        }

        foreach (var grupo in resultado.GruposAssunto)
        {
            if (grupo.CriarNovo)
            {
                if (string.IsNullOrWhiteSpace(grupo.NomeDisciplinaFinal) || string.IsNullOrWhiteSpace(grupo.NomeAssuntoFinal))
                {
                    throw new OperacaoInvalidaException($"Informe o nome da Disciplina e do Assunto para o grupo \"{grupo.Disciplina} / {grupo.Assunto}\".");
                }
            }
            else if (grupo.AssuntoIdEscolhido == 0)
            {
                throw new OperacaoInvalidaException($"Escolha um Assunto existente para o grupo \"{grupo.Disciplina} / {grupo.Assunto}\", ou opte por criar um novo.");
            }
        }

        // Transação: grupos "CriarNovo" geram vários SaveChanges — sem ela, um
        // erro no meio deixaria Disciplinas/Assuntos criados sem as Questões.
        using var transacao = await db.Database.BeginTransactionAsync();

        // Carregado uma vez, fora do laço, pra resolver o(s) código(s) por Codigo textual.
        AreaCurso? areaDoCurso = null;
        ILookup<string, ItemMatrizReferencia>? itensPorCodigo = null;
        if (cursoId is not (null or 0))
        {
            var (itens, area) = await ObterItensEAreaDoCursoAsync(cursoId.Value);
            itensPorCodigo = itens.ToLookup(i => i.Codigo, StringComparer.OrdinalIgnoreCase);
            areaDoCurso = area;
        }

        var importadas = 0;
        foreach (var q in resultado.Questoes.Where(q => q.Selecionada))
        {
            Questao nova = q.Tipo switch
            {
                TipoQuestao.MultiplaEscolha => new QuestaoMultiplaEscolha
                {
                    Enunciado = q.Enunciado,
                    RespostaCorreta = (char)('A' + Math.Max(0, q.Alternativas.FindIndex(a => a.Correta))),
                    Alternativas = q.Alternativas
                        .Select((a, i) => new AlternativaQuestao { Letra = (char)('A' + i), Texto = a.Texto })
                        .ToList(),
                },
                TipoQuestao.CertoErrado => new QuestaoCertoErrado
                {
                    Enunciado = q.Enunciado,
                    RespostaCorreta = q.RespostaCertoErrado ?? true,
                },
                TipoQuestao.RespostaBreve => new QuestaoRespostaBreve
                {
                    Enunciado = q.Enunciado,
                    RespostaEsperada = q.RespostaBreveEsperada ?? "",
                },
                TipoQuestao.Numerica => new QuestaoNumerica
                {
                    Enunciado = q.Enunciado,
                    RespostaEsperada = q.NumericaEsperada ?? 0,
                    Tolerancia = q.NumericaTolerancia ?? 0,
                },
                TipoQuestao.Associacao => new QuestaoAssociacao
                {
                    Enunciado = q.Enunciado,
                    Pares = q.Pares
                        .Select((p, i) => new ParAssociacao { Termo = p.Termo, Correspondente = p.Correspondente, Ordem = i })
                        .ToList(),
                },
                // GIFT ("{}" vazio) nunca traz resposta esperada pra Discursiva — nasce em
                // branco de propósito, o professor completa depois (ver GiftParser.Aviso).
                TipoQuestao.Discursiva => new QuestaoDiscursiva
                {
                    Enunciado = q.Enunciado,
                    RespostaEsperada = "",
                },
                _ => throw new InvalidOperationException("Tipo de questão importada inválido."),
            };

            nova.AssuntoId = q.AssuntoSugerido is null
                ? assuntoId
                : await ResolverAssuntoIdDoGrupoAsync(resultado, q, criadoPorId);
            nova.Dificuldade = dificuldade;
            nova.TipoQuestao = q.Tipo;
            nova.CriadoPorId = criadoPorId;
            nova.Origem = OrigemQuestao.Importada;

            // cursoId é só escopo transitório. Item nacional também adiciona a AreaCurso do Curso.
            if (itensPorCodigo is not null && q.CodigosItemMatriz.Count > 0)
            {
                var itens = q.CodigosItemMatriz
                    .SelectMany(c => itensPorCodigo[c])
                    .DistinctBy(i => i.Id)
                    .ToList();
                if (itens.Count > 0)
                {
                    nova.ItensMatriz = itens;
                    if (areaDoCurso is not null && itens.Any(i => i.MatrizReferencia?.AreaCursoId is not null))
                    {
                        nova.AreasCurso.Add(areaDoCurso);
                    }
                }
            }

            db.Questoes.Add(nova);
            importadas++;
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();
        return importadas;
    }

    // Aplica a decisão do grupo: usa AssuntoIdEscolhido, ou cria
    // Disciplina/Assunto na 1ª vez (próximas questões reaproveitam AssuntoIdCriado).
    private async Task<int> ResolverAssuntoIdDoGrupoAsync(ResultadoImportacao resultado, QuestaoImportada q, string? criadoPorId)
    {
        var grupo = resultado.GruposAssunto.FirstOrDefault(g =>
            g.Disciplina == q.DisciplinaSugerida && g.Assunto == q.AssuntoSugerido)
            ?? throw new InvalidOperationException(
                $"Questão com \"$ASSUNTO: {q.DisciplinaSugerida} / {q.AssuntoSugerido}\" sem grupo correspondente — chame ResolverAssuntosAsync antes de importar.");

        if (!grupo.CriarNovo)
        {
            return grupo.AssuntoIdEscolhido;
        }

        if (grupo.AssuntoIdCriado is int jaCriado)
        {
            return jaCriado;
        }

        var disciplinaId = grupo.DisciplinaIdEncontrada
            ?? (await disciplinaService.CriarAsync(grupo.NomeDisciplinaFinal, criadoPorId)).Id;

        var assunto = await disciplinaService.CriarAssuntoAsync(grupo.NomeAssuntoFinal, disciplinaId);
        grupo.AssuntoIdCriado = assunto.Id;
        return assunto.Id;
    }
}
