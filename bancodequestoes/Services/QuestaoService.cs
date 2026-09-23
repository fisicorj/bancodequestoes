using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Mutação central de Questões: criar/editar/ativar/desativar/excluir/duplicar.
// Listagem vive em QuestaoQueryService; Tag/Histórico/Imagem/Curricular são Services à parte, injetados aqui.
public class QuestaoService(
    ApplicationDbContext db,
    QuestaoCurricularService curricular,
    QuestaoImagemService imagens,
    QuestaoTagService tags,
    QuestaoHistoricoService historico,
    SugestaoIaService sugestaoIa)
{
    // --- Leitura ---

    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    // Editar continua exclusivo de quem criou, independente da Visibilidade
    // (que só controla quem pode USAR a questão numa prova).
    public async Task<Questao> CarregarParaEdicaoAsync(int id, string? meuId)
    {
        var questao = await CarregarComFilhosAsync(id)
            ?? throw new OperacaoInvalidaException("Questão não encontrada.");

        if (questao.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você não tem permissão para editar esta questão (ela pertence a outro professor).");
        }

        return questao;
    }

    // Usado por prévia/duplicar/exportar — qualquer professor que enxerga a questão
    // pode, não só quem criou; null tanto pra "não existe" quanto "não é visível".
    public async Task<Questao?> CarregarVisivelAsync(int id, string? meuId, int? minhaInstituicaoId)
    {
        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == id);

        return visivel ? await CarregarComFilhosAsync(id) : null;
    }

    // Loader cru, sem checar autorização — só pode ser chamado depois que
    // CarregarParaEdicaoAsync/CarregarVisivelAsync já decidiu o acesso.
    private async Task<Questao?> CarregarComFilhosAsync(int id)
    {
        var questao = await db.Questoes
            .Include(q => q.Assunto)
                .ThenInclude(a => a!.Disciplina)
            .Include(q => q.Tags)
            .Include(q => q.ItensMatriz)
                .ThenInclude(i => i.MatrizReferencia)
            .Include(q => q.AreasCurso)
            // Três coleções incluídas ao mesmo tempo — ver AsSplitQuery em QuestaoQueryService.ListarAsync.
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == id);

        if (questao is null)
        {
            return null;
        }

        if (questao is QuestaoMultiplaEscolha me)
        {
            await db.Entry(me).Collection(m => m.Alternativas).LoadAsync();
        }
        if (questao is QuestaoAssociacao assoc)
        {
            await db.Entry(assoc).Collection(a => a.Pares).LoadAsync();
        }
        if (questao is QuestaoLacunas lac)
        {
            await db.Entry(lac).Collection(l => l.Lacunas).LoadAsync();
        }
        await db.Entry(questao).Collection(q => q.Imagens).LoadAsync();

        return questao;
    }

    // --- Mutação ---

    // Só classifica questões SEM Bloom, nunca sobrescreve: tenta a heurística
    // (ClassificadorBloom) primeiro; só com usarIa=true o resto vai pro SugestaoIaService.
    public async Task<(int Classificadas, int SemSugestao)> ClassificarBloomAutomaticamenteAsync(string? meuId, bool usarIa = false)
    {
        var candidatas = await db.Questoes
            .Where(q => q.CriadoPorId == meuId && q.Bloom == null)
            .ToListAsync();

        var classificadas = 0;
        foreach (var questao in candidatas)
        {
            var nivel = ClassificadorBloom.Classificar(questao.Enunciado);
            if (nivel is null && usarIa)
            {
                nivel = await sugestaoIa.SugerirBloomAsync(questao.Enunciado);
            }

            if (nivel is not null)
            {
                questao.Bloom = nivel;
                questao.AtualizadoEm = DateTime.UtcNow;
                classificadas++;
            }
        }

        if (classificadas > 0)
        {
            await db.SaveChangesAsync();
        }

        return (classificadas, candidatas.Count - classificadas);
    }

    public async Task<Questao> CriarAsync(QuestaoInput modelo, List<PendenteImagem> imagensNovas, string? criadoPorId, int? minhaInstituicaoId)
    {
        ValidarModelo(modelo, minhaInstituicaoId);
        await curricular.ValidarConsistenciaEnadeAsync(modelo);

        Questao nova = ConstruirPorTipo(modelo);
        nova.AssuntoId = modelo.AssuntoId;
        nova.Dificuldade = modelo.Dificuldade;
        nova.TipoQuestao = modelo.TipoQuestao;
        nova.Visibilidade = modelo.Visibilidade;
        nova.Bloom = modelo.Bloom;
        nova.Origem = modelo.Origem;
        nova.Ano = modelo.Ano;
        nova.Referencia = string.IsNullOrWhiteSpace(modelo.Referencia) ? null : modelo.Referencia;
        nova.SecaoEnade = modelo.SecaoEnade;
        nova.NumeroOriginal = string.IsNullOrWhiteSpace(modelo.NumeroOriginal) ? null : modelo.NumeroOriginal.Trim();
        nova.CodigoProvaOrigem = string.IsNullOrWhiteSpace(modelo.CodigoProvaOrigem) ? null : modelo.CodigoProvaOrigem.Trim();
        nova.Explicacao = string.IsNullOrWhiteSpace(modelo.Explicacao) ? null : modelo.Explicacao;
        nova.Tags = await tags.ResolverTagsAsync(modelo.Tags);
        nova.CriadoPorId = criadoPorId;
        // CursoContextoMatrizId nunca é persistido — só contexto transitório pra resolver
        // Itens de Matriz institucionais; idsJaVinculadosAntes vazio (questão nova).
        nova.ItensMatriz = await curricular.ResolverItensMatrizAsync(modelo.CursoContextoMatrizId, modelo.AreaCursoIds, modelo.ItemMatrizIds, idsJaVinculadosAntes: new HashSet<int>());
        nova.AreasCurso = await curricular.ResolverAreasCursoAsync(modelo.AreaCursoIds);

        var imagensPorToken = imagens.AdicionarPendentes(nova.Imagens, imagensNovas, ordemInicial: 0);

        await tags.SincronizarTagDeImagemAsync(nova);

        db.Questoes.Add(nova);
        await db.SaveChangesAsync();

        if (imagensPorToken.Count > 0)
        {
            var enunciadoReescrito = QuestaoImagemService.ReescreverReferenciasPendentes(nova.Enunciado, imagensPorToken);
            if (enunciadoReescrito != nova.Enunciado)
            {
                nova.Enunciado = enunciadoReescrito;
                await db.SaveChangesAsync();
            }
        }

        return nova;
    }

    public async Task AtualizarAsync(
        int id,
        QuestaoInput modelo,
        List<QuestaoImagem> imagensExistentesEditadas,
        HashSet<int> imagensParaRemover,
        List<PendenteImagem> imagensNovas,
        string? meuId,
        int? minhaInstituicaoId)
    {
        ValidarModelo(modelo, minhaInstituicaoId);
        await curricular.ValidarConsistenciaEnadeAsync(modelo);

        var questao = await CarregarParaEdicaoAsync(id, meuId);

        questao.AssuntoId = modelo.AssuntoId;
        questao.Enunciado = modelo.Enunciado;
        questao.Dificuldade = modelo.Dificuldade;
        questao.Visibilidade = modelo.Visibilidade;
        questao.Bloom = modelo.Bloom;
        questao.Origem = modelo.Origem;
        questao.Ano = modelo.Ano;
        questao.Referencia = string.IsNullOrWhiteSpace(modelo.Referencia) ? null : modelo.Referencia;
        questao.SecaoEnade = modelo.SecaoEnade;
        questao.NumeroOriginal = string.IsNullOrWhiteSpace(modelo.NumeroOriginal) ? null : modelo.NumeroOriginal.Trim();
        questao.CodigoProvaOrigem = string.IsNullOrWhiteSpace(modelo.CodigoProvaOrigem) ? null : modelo.CodigoProvaOrigem.Trim();
        questao.Explicacao = string.IsNullOrWhiteSpace(modelo.Explicacao) ? null : modelo.Explicacao;
        questao.Tags = await tags.ResolverTagsAsync(modelo.Tags);
        questao.Ativa = modelo.Ativa;
        questao.AtualizadoEm = DateTime.UtcNow;

        // Reatribuir a coleção inteira resolve a troca de Curso (item de outro Curso é
        // REJEITADO); idsJaVinculadosAntes poupa vínculo histórico do check de Ativo/Rascunho.
        var idsJaVinculadosAntes = questao.ItensMatriz.Select(i => i.Id).ToHashSet();
        questao.ItensMatriz = await curricular.ResolverItensMatrizAsync(modelo.CursoContextoMatrizId, modelo.AreaCursoIds, modelo.ItemMatrizIds, idsJaVinculadosAntes);

        // Mesmo raciocínio de reatribuir a coleção inteira — o EF Core
        // calcula o diff de QuestaoAreaCurso a inserir/remover sozinho.
        questao.AreasCurso = await curricular.ResolverAreasCursoAsync(modelo.AreaCursoIds);

        switch (questao)
        {
            case QuestaoMultiplaEscolha me:
                db.AlternativasQuestao.RemoveRange(me.Alternativas);
                me.Alternativas = modelo.Alternativas
                    .Select((a, i) => new AlternativaQuestao { Letra = (char)('A' + i), Texto = a.Texto })
                    .ToList();
                me.RespostaCorreta = (char)('A' + modelo.RespostaCorretaIndex);
                break;
            case QuestaoDiscursiva d:
                d.RespostaEsperada = modelo.RespostaEsperada;
                d.CriterioAvaliacao = string.IsNullOrWhiteSpace(modelo.CriterioAvaliacao) ? null : modelo.CriterioAvaliacao;
                break;
            case QuestaoCertoErrado c:
                c.RespostaCorreta = modelo.RespostaCorretaCE;
                break;
            case QuestaoAssociacao assoc:
                db.ParesAssociacao.RemoveRange(assoc.Pares);
                assoc.Pares = modelo.Pares
                    .Select((p, i) => new ParAssociacao { Termo = p.Termo, Correspondente = p.Correspondente, Ordem = i })
                    .ToList();
                break;
            case QuestaoRespostaBreve rb:
                rb.RespostaEsperada = modelo.RespostaBreveEsperada;
                break;
            case QuestaoNumerica num:
                num.RespostaEsperada = modelo.NumericaEsperada ?? 0;
                num.Tolerancia = modelo.NumericaTolerancia ?? 0;
                break;
            case QuestaoLacunas lac:
                db.LacunasRespostas.RemoveRange(lac.Lacunas);
                lac.Lacunas = modelo.Lacunas
                    .Select((l, i) => new LacunaResposta { RespostaEsperada = l.RespostaEsperada, Ordem = i })
                    .ToList();
                break;
        }

        imagens.AplicarEdicoesExistentes(questao, imagensExistentesEditadas);
        imagens.Remover(questao, imagensParaRemover);

        // Só as imagens vindas do botão "Imagem" (Token preenchido) precisam
        // ser rastreadas, pra reescrever "imagem:pendente:{token}" pro Id real depois do save.
        var imagensPorToken = imagens.AdicionarPendentes(questao.Imagens, imagensNovas, ordemInicial: questao.Imagens.Count);

        // Depois de todas as adições/remoções — reflete o estado FINAL de Imagens.
        await tags.SincronizarTagDeImagemAsync(questao);

        // Por último: compara contra o snapshot original de todas as mutações acima.
        await historico.RegistrarEdicaoAsync(questao, meuId);

        await db.SaveChangesAsync();

        if (imagensPorToken.Count > 0)
        {
            var enunciadoReescrito = QuestaoImagemService.ReescreverReferenciasPendentes(questao.Enunciado, imagensPorToken);
            if (enunciadoReescrito != questao.Enunciado)
            {
                questao.Enunciado = enunciadoReescrito;
                await db.SaveChangesAsync();
            }
        }
    }

    public async Task AlternarAtivaAsync(Questao q, string? meuId)
    {
        if (q.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você só pode ativar/desativar questões que você criou.");
        }

        q.Ativa = !q.Ativa;
        q.AtualizadoEm = DateTime.UtcNow;
        await historico.RegistrarEdicaoAsync(q, meuId);
        await db.SaveChangesAsync();
    }

    // Desativar é o caminho padrão pra "remover" sem perder histórico —
    // exclusão física é bloqueada pelo banco (FK Restrict) se vinculada a alguma prova.
    public async Task ExcluirAsync(Questao q, string? meuId)
    {
        if (q.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você só pode excluir questões que você criou.");
        }

        try
        {
            db.Questoes.Remove(q);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                "Não foi possível excluir essa questão: ela está vinculada a uma ou mais provas. Remova-a das provas antes de excluir, ou apenas desative-a.");
        }
    }

    public async Task<Questao> DuplicarAsync(int id, string? userId, int? minhaInstituicaoId)
    {
        // Duplicar é uma forma de "usar" — mesma regra de quem pode enxergar a questão original, não só quem criou.
        var completa = await CarregarVisivelAsync(id, userId, minhaInstituicaoId)
            ?? throw new OperacaoInvalidaException("Questão não encontrada.");

        Questao copia = completa switch
        {
            QuestaoMultiplaEscolha me => new QuestaoMultiplaEscolha
            {
                Enunciado = "",
                RespostaCorreta = me.RespostaCorreta,
                Alternativas = me.Alternativas.Select(a => new AlternativaQuestao { Letra = a.Letra, Texto = a.Texto }).ToList(),
            },
            QuestaoDiscursiva d => new QuestaoDiscursiva { Enunciado = "", RespostaEsperada = d.RespostaEsperada, CriterioAvaliacao = d.CriterioAvaliacao },
            QuestaoCertoErrado c => new QuestaoCertoErrado { Enunciado = "", RespostaCorreta = c.RespostaCorreta },
            QuestaoAssociacao assoc => new QuestaoAssociacao
            {
                Enunciado = "",
                Pares = assoc.Pares.Select(p => new ParAssociacao { Termo = p.Termo, Correspondente = p.Correspondente, Ordem = p.Ordem }).ToList(),
            },
            QuestaoRespostaBreve rb => new QuestaoRespostaBreve { Enunciado = "", RespostaEsperada = rb.RespostaEsperada },
            QuestaoNumerica num => new QuestaoNumerica { Enunciado = "", RespostaEsperada = num.RespostaEsperada, Tolerancia = num.Tolerancia },
            QuestaoLacunas lac => new QuestaoLacunas
            {
                Enunciado = "",
                Lacunas = lac.Lacunas.Select(l => new LacunaResposta { Ordem = l.Ordem, RespostaEsperada = l.RespostaEsperada }).ToList(),
            },
            _ => throw new InvalidOperationException("Tipo de questão inválido."),
        };

        copia.Enunciado = completa.Enunciado + " (cópia)";
        copia.AssuntoId = completa.AssuntoId;
        copia.Dificuldade = completa.Dificuldade;
        copia.TipoQuestao = completa.TipoQuestao;
        copia.Bloom = completa.Bloom;
        copia.Origem = completa.Origem;
        copia.Ano = completa.Ano;
        copia.Referencia = completa.Referencia;
        copia.SecaoEnade = completa.SecaoEnade;
        copia.NumeroOriginal = completa.NumeroOriginal;
        copia.CodigoProvaOrigem = completa.CodigoProvaOrigem;
        copia.Explicacao = completa.Explicacao;
        copia.Tags = completa.Tags.ToList();
        copia.CriadoPorId = userId;
        copia.ItensMatriz = completa.ItensMatriz.ToList();
        copia.AreasCurso = completa.AreasCurso.ToList();
        copia.Imagens = completa.Imagens
            .Select(im => new QuestaoImagem
            {
                Conteudo = im.Conteudo,
                ContentType = im.ContentType,
                NomeArquivo = im.NomeArquivo,
                Ordem = im.Ordem,
                Legenda = im.Legenda,
                TextoAlternativo = im.TextoAlternativo,
                Alinhamento = im.Alinhamento,
                LarguraPercentual = im.LarguraPercentual,
            })
            .ToList();

        db.Questoes.Add(copia);
        await db.SaveChangesAsync();
        return copia;
    }

    private static Questao ConstruirPorTipo(QuestaoInput modelo) => modelo.TipoQuestao switch
    {
        TipoQuestao.MultiplaEscolha => new QuestaoMultiplaEscolha
        {
            Enunciado = modelo.Enunciado,
            RespostaCorreta = (char)('A' + modelo.RespostaCorretaIndex),
            Alternativas = modelo.Alternativas
                .Select((a, i) => new AlternativaQuestao { Letra = (char)('A' + i), Texto = a.Texto })
                .ToList(),
        },
        TipoQuestao.Discursiva => new QuestaoDiscursiva
        {
            Enunciado = modelo.Enunciado,
            RespostaEsperada = modelo.RespostaEsperada,
            CriterioAvaliacao = string.IsNullOrWhiteSpace(modelo.CriterioAvaliacao) ? null : modelo.CriterioAvaliacao,
        },
        TipoQuestao.CertoErrado => new QuestaoCertoErrado
        {
            Enunciado = modelo.Enunciado,
            RespostaCorreta = modelo.RespostaCorretaCE,
        },
        TipoQuestao.Associacao => new QuestaoAssociacao
        {
            Enunciado = modelo.Enunciado,
            Pares = modelo.Pares
                .Select((p, i) => new ParAssociacao { Termo = p.Termo, Correspondente = p.Correspondente, Ordem = i })
                .ToList(),
        },
        TipoQuestao.RespostaBreve => new QuestaoRespostaBreve
        {
            Enunciado = modelo.Enunciado,
            RespostaEsperada = modelo.RespostaBreveEsperada,
        },
        TipoQuestao.Numerica => new QuestaoNumerica
        {
            Enunciado = modelo.Enunciado,
            RespostaEsperada = modelo.NumericaEsperada ?? 0,
            Tolerancia = modelo.NumericaTolerancia ?? 0,
        },
        TipoQuestao.Lacunas => new QuestaoLacunas
        {
            Enunciado = modelo.Enunciado,
            Lacunas = modelo.Lacunas
                .Select((l, i) => new LacunaResposta { RespostaEsperada = l.RespostaEsperada, Ordem = i })
                .ToList(),
        },
        _ => throw new InvalidOperationException("Tipo de questão inválido."),
    };

    // Validações por tipo + defesa em profundidade contra Institucional sem
    // instituição — Enunciado é validado à parte pelo EditForm.
    private static void ValidarModelo(QuestaoInput modelo, int? minhaInstituicaoId)
    {
        if (modelo.AssuntoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione um assunto.");
        }

        // O <select> já desabilita "Institucional" sem instituição, mas o
        // valor pode chegar aqui mesmo assim.
        if (modelo.Visibilidade == VisibilidadeQuestao.Institucional && !minhaInstituicaoId.HasValue)
        {
            throw new OperacaoInvalidaException("Você precisa definir sua instituição no perfil antes de usar essa opção de visibilidade.");
        }

        if (modelo.TipoQuestao == TipoQuestao.MultiplaEscolha)
        {
            if (modelo.Alternativas.Any(a => string.IsNullOrWhiteSpace(a.Texto)))
            {
                throw new OperacaoInvalidaException("Preencha o texto de todas as alternativas.");
            }
            if (modelo.RespostaCorretaIndex < 0 || modelo.RespostaCorretaIndex >= modelo.Alternativas.Count)
            {
                modelo.RespostaCorretaIndex = 0;
            }
        }

        if (modelo.TipoQuestao == TipoQuestao.Discursiva && string.IsNullOrWhiteSpace(modelo.RespostaEsperada))
        {
            throw new OperacaoInvalidaException("Informe a resposta esperada.");
        }

        if (modelo.TipoQuestao == TipoQuestao.Associacao && modelo.Pares.Any(p => string.IsNullOrWhiteSpace(p.Termo) || string.IsNullOrWhiteSpace(p.Correspondente)))
        {
            throw new OperacaoInvalidaException("Preencha todos os pares (termo e correspondente).");
        }

        if (modelo.TipoQuestao == TipoQuestao.RespostaBreve && string.IsNullOrWhiteSpace(modelo.RespostaBreveEsperada))
        {
            throw new OperacaoInvalidaException("Informe a resposta esperada.");
        }

        if (modelo.TipoQuestao == TipoQuestao.Numerica && modelo.NumericaEsperada is null)
        {
            throw new OperacaoInvalidaException("Informe a resposta numérica esperada.");
        }

        if (modelo.TipoQuestao == TipoQuestao.Lacunas && modelo.Lacunas.Any(l => string.IsNullOrWhiteSpace(l.RespostaEsperada)))
        {
            throw new OperacaoInvalidaException("Preencha a resposta esperada de todas as lacunas.");
        }
    }
}
