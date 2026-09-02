using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Concentra consulta EF + regra de negócio de Questões: listagem/filtros,
// estatística de uso, criação/edição (validação por tipo incluída), ativar/
// desativar, excluir, duplicar. QuestaoForm.razor e QuestaoList.razor ficam
// só com UI + binding — mesmo padrão do DisciplinaService.
//
// GIFT/Aiken (import/export) e a lógica de exportação em Exportacao/ NÃO
// entram aqui de propósito — ficam pra ImportacaoService/ExportacaoService,
// os próximos passos dessa refatoração.
public class QuestaoService(ApplicationDbContext db, MatrizReferenciaService matrizService)
{
    // --- Leitura ---

    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    public Task<List<Tag>> ListarTagsAsync() =>
        db.Tags.OrderBy(t => t.Nome).ToListAsync();

    // Serve o binário da imagem só se a questão dona dela for VISÍVEL pra quem
    // pediu (mesma regra usada pra listar questões) — sem isso, qualquer
    // usuário autenticado poderia adivinhar/incrementar o id na URL
    // (/questoes/imagem/1, /2, /3...) e ver imagens de questões privadas de
    // outro professor, mesmo sem conseguir abrir a questão em si.
    public async Task<QuestaoImagem?> ObterImagemVisivelAsync(int imagemId, string? meuId, int? minhaInstituicaoId)
    {
        var imagem = await db.QuestoesImagens.FindAsync(imagemId);
        if (imagem is null)
        {
            return null;
        }

        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == imagem.QuestaoId);

        return visivel ? imagem : null;
    }

    public async Task<PaginaResultado<Questao>> ListarAsync(QuestaoFiltro filtro, string? meuId, int? minhaInstituicaoId, int pagina, int tamanhoPagina)
    {
        var query = db.Questoes
            .Include(q => q.Assunto)
                .ThenInclude(a => a!.Disciplina)
            .Include(q => q.Tags)
            .Include(q => q.Imagens)
            .Include(q => q.ItensMatriz)
                .ThenInclude(i => i.MatrizReferencia)
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

        // "E" entre as tags selecionadas (precisa ter TODAS, não só uma) — um
        // Where encadeado por tag vira uma subquery EXISTS por tag, o que dá
        // exatamente essa semântica de interseção.
        foreach (var tagId in filtro.TagIds)
        {
            query = query.Where(q => q.Tags.Any(t => t.Id == tagId));
        }

        if (filtro.CursoId != 0)
        {
            query = query.Where(q => q.CursoId == filtro.CursoId);
        }

        if (filtro.MatrizReferenciaId != 0)
        {
            query = query.Where(q => q.ItensMatriz.Any(i => i.MatrizReferenciaId == filtro.MatrizReferenciaId));
        }

        // Item de matriz é flat (sem hierarquia, diferente do antigo modelo
        // de Diretriz) — o filtro é direto por Id, sem precisar expandir
        // descendentes. Continua acontecendo no banco (Any vira EXISTS no
        // SQL), nunca carregando as questões pra memória pra filtrar depois.
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

        // A coleção específica do tipo (Alternativas/Pares/Lacunas) não dá pra
        // trazer com um .Include comum aqui porque ela só existe na subclasse,
        // não em Questao — carregamos uma por uma nas até 20 questões da
        // página (mesma técnica de CarregarComFilhosAsync), só pra alimentar o
        // farol de qualidade (ver QualidadeQuestao) mostrado em cada card.
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

    // Uma consulta só serve pros dois: a contagem ("Usada N vezes") e a
    // "última utilização" (prova + tipo + data), que precisa do registro mais
    // recente por questão — daí trazer tudo pra memória e agrupar em C# em
    // vez de duas idas ao banco.
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

        // Sem Data da aplicação definida, cai pra CriadoEm — assim uma prova
        // recém-criada (mas ainda sem data marcada) continua contando como o
        // uso mais recente, em vez de ficar sempre por último no ranking.
        var ultimoUso = usos
            .GroupBy(u => u.QuestaoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(u => u.DataAplicacao?.ToDateTime(TimeOnly.MinValue) ?? u.CriadoEm)
                    .Select(u => new UltimoUsoInfo { Titulo = u.Titulo, Tipo = u.Tipo, Data = u.DataAplicacao })
                    .First());

        return (contagem, ultimoUso);
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

    // Carrega uma questão só se ela pertencer a quem está pedindo — editar
    // continua exclusivo de quem criou, independente da Visibilidade
    // (Institucional/Compartilhada só controlam quem PODE USAR a questão
    // numa prova, não quem pode editá-la — senão dois professores poderiam
    // brigar pela mesma questão compartilhada). Lança
    // OperacaoInvalidaException tanto pra "não encontrada" quanto pra "não é
    // sua" — a tela não precisa (e não pode esquecer de) checar isso de novo.
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

    // Carrega uma questão só se ela for VISÍVEL pra quem está pedindo (mesma
    // regra usada pra listar questões) — usado pra prévia, duplicar e
    // exportar GIFT/Aiken, que qualquer professor que enxerga a questão pode
    // fazer, não só quem criou. Devolve null tanto pra "não existe" quanto
    // pra "existe mas não é visível" — de propósito, pra não revelar a um
    // usuário sem permissão que aquele id corresponde a uma questão real.
    public async Task<Questao?> CarregarVisivelAsync(int id, string? meuId, int? minhaInstituicaoId)
    {
        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == id);

        return visivel ? await CarregarComFilhosAsync(id) : null;
    }

    // Loader "cru" (Assunto+Disciplina, Tags, coleções do subtipo, Imagens) —
    // privado de propósito: carrega qualquer questão do sistema sem checar
    // quem está pedindo, então só pode ser chamado depois que
    // CarregarParaEdicaoAsync/CarregarVisivelAsync (ou uma checagem
    // equivalente) já decidiu que o acesso é permitido.
    private async Task<Questao?> CarregarComFilhosAsync(int id)
    {
        var questao = await db.Questoes
            .Include(q => q.Assunto)
                .ThenInclude(a => a!.Disciplina)
            .Include(q => q.Tags)
            .Include(q => q.Curso)
            .Include(q => q.ItensMatriz)
                .ThenInclude(i => i.MatrizReferencia)
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

    // Resolve cada nome de tag digitado pra uma Tag já existente (buscando
    // sem diferenciar maiúscula/minúscula) ou cria uma nova — evita duplicar
    // "Pipeline" e "pipeline" como tags diferentes no vocabulário compartilhado.
    public async Task<List<Tag>> ResolverTagsAsync(List<string> nomes)
    {
        var resultado = new List<Tag>();
        foreach (var nome in nomes.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resultado.Add(await ObterOuCriarTagAsync(nome));
        }
        return resultado;
    }

    private async Task<Tag> ObterOuCriarTagAsync(string nome)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => EF.Functions.ILike(t.Nome, nome));
        if (tag is null)
        {
            tag = new Tag { Nome = nome };
            db.Tags.Add(tag);
        }
        return tag;
    }

    // Tag automática: toda questão com pelo menos uma imagem ganha essa tag,
    // e perde quando a última imagem é removida — mantém o vocabulário
    // sincronizado com o conteúdo real, sem o professor precisar lembrar de
    // marcar isso manualmente. Nunca mexe nas outras tags que o professor
    // digitou (ResolverTagsAsync já rodou antes de isso ser chamado).
    private const string TagComImagem = "Com imagem";

    private async Task SincronizarTagDeImagemAsync(Questao questao)
    {
        var temImagem = questao.Imagens.Count > 0;
        var tagAtual = questao.Tags.FirstOrDefault(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));

        if (temImagem && tagAtual is null)
        {
            questao.Tags.Add(await ObterOuCriarTagAsync(TagComImagem));
        }
        else if (!temImagem && tagAtual is not null)
        {
            questao.Tags.Remove(tagAtual);
        }
    }

    // Backfill: conserta questões que já tinham imagem antes dessa
    // sincronização automática existir. Idempotente — seguro rodar de novo
    // (só mexe em quem realmente precisa), então dá pra chamar toda vez que
    // o app sobe, igual o DbSeeder.RepararQuestoesSemDonoAsync.
    public async Task<int> SincronizarTagsDeImagemEmMassaAsync()
    {
        var questoes = await db.Questoes
            .Include(q => q.Imagens)
            .Include(q => q.Tags)
            .ToListAsync();

        var alteradas = 0;
        foreach (var questao in questoes)
        {
            var tagAntes = questao.Tags.Any(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));
            await SincronizarTagDeImagemAsync(questao);
            var tagDepois = questao.Tags.Any(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));

            if (tagAntes != tagDepois)
            {
                alteradas++;
            }
        }

        if (alteradas > 0)
        {
            await db.SaveChangesAsync();
        }

        return alteradas;
    }

    // Sugere um nível de Bloom (heurística por palavras-chave no enunciado —
    // ver ClassificadorBloom) só pras questões do professor que ainda estão
    // SEM Bloom definido — nunca sobrescreve uma classificação já feita,
    // manual ou de uma rodada anterior desse mesmo botão. É uma sugestão
    // aproximada, não uma verdade absoluta: revisar depois em QuestaoForm
    // continua valendo, e questões sem verbo reconhecível ficam sem Bloom em
    // vez de receber um palpite ruim.
    public async Task<(int Classificadas, int SemSugestao)> ClassificarBloomAutomaticamenteAsync(string? meuId)
    {
        var candidatas = await db.Questoes
            .Where(q => q.CriadoPorId == meuId && q.Bloom == null)
            .ToListAsync();

        var classificadas = 0;
        foreach (var questao in candidatas)
        {
            var nivel = ClassificadorBloom.Classificar(questao.Enunciado);
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

        Questao nova = ConstruirPorTipo(modelo);
        nova.AssuntoId = modelo.AssuntoId;
        nova.Dificuldade = modelo.Dificuldade;
        nova.TipoQuestao = modelo.TipoQuestao;
        nova.Visibilidade = modelo.Visibilidade;
        nova.Bloom = modelo.Bloom;
        nova.Origem = modelo.Origem;
        nova.Ano = modelo.Ano;
        nova.Referencia = string.IsNullOrWhiteSpace(modelo.Referencia) ? null : modelo.Referencia;
        nova.Explicacao = string.IsNullOrWhiteSpace(modelo.Explicacao) ? null : modelo.Explicacao;
        nova.Tags = await ResolverTagsAsync(modelo.Tags);
        nova.CriadoPorId = criadoPorId;
        nova.CursoId = modelo.CursoId;
        nova.ItensMatriz = await matrizService.ValidarItensDoCursoAsync(modelo.CursoId, modelo.ItemMatrizIds);

        var ordem = 0;
        foreach (var p in imagensNovas)
        {
            nova.Imagens.Add(NovaImagemDePendente(p, ordem++));
        }

        await SincronizarTagDeImagemAsync(nova);

        db.Questoes.Add(nova);
        await db.SaveChangesAsync();
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

        var questao = await CarregarParaEdicaoAsync(id, meuId);

        questao.AssuntoId = modelo.AssuntoId;
        questao.Enunciado = modelo.Enunciado;
        questao.Dificuldade = modelo.Dificuldade;
        questao.Visibilidade = modelo.Visibilidade;
        questao.Bloom = modelo.Bloom;
        questao.Origem = modelo.Origem;
        questao.Ano = modelo.Ano;
        questao.Referencia = string.IsNullOrWhiteSpace(modelo.Referencia) ? null : modelo.Referencia;
        questao.Explicacao = string.IsNullOrWhiteSpace(modelo.Explicacao) ? null : modelo.Explicacao;
        questao.Tags = await ResolverTagsAsync(modelo.Tags);
        questao.Ativa = modelo.Ativa;
        questao.AtualizadoEm = DateTime.UtcNow;

        // Reatribuir a coleção inteira (em vez de tentar diffar manualmente)
        // já resolve sozinho o caso "trocou de curso": ValidarItensDoCursoAsync
        // só devolve itens do CursoId atual, então vínculos do curso antigo
        // somem daqui — sem isso, uma questão que mudasse de curso ficaria
        // com QuestaoItemMatriz apontando pra itens de um curso que não é
        // mais o dela. questao.ItensMatriz já vem carregada (ver Include em
        // CarregarComFilhosAsync), então o EF Core consegue calcular o diff
        // de linhas de junção a inserir/remover.
        questao.CursoId = modelo.CursoId;
        questao.ItensMatriz = await matrizService.ValidarItensDoCursoAsync(modelo.CursoId, modelo.ItemMatrizIds);

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

        // As edições de Legenda/Texto alternativo/Alinhamento/Largura em imagens
        // já existentes acontecem direto nos objetos que a tela mantém (bind na
        // tela) — sincroniza por Id em vez de assumir que são as MESMAS
        // instâncias rastreadas pelo DbContext.
        foreach (var imgExistente in imagensExistentesEditadas)
        {
            var alvo = questao.Imagens.FirstOrDefault(i => i.Id == imgExistente.Id);
            if (alvo is not null)
            {
                alvo.Legenda = string.IsNullOrWhiteSpace(imgExistente.Legenda) ? null : imgExistente.Legenda;
                alvo.TextoAlternativo = string.IsNullOrWhiteSpace(imgExistente.TextoAlternativo) ? null : imgExistente.TextoAlternativo;
                alvo.Alinhamento = imgExistente.Alinhamento;
                alvo.LarguraPercentual = imgExistente.LarguraPercentual;
            }
        }

        foreach (var idRemover in imagensParaRemover)
        {
            var img = questao.Imagens.FirstOrDefault(i => i.Id == idRemover);
            if (img is not null)
            {
                questao.Imagens.Remove(img);
                db.QuestoesImagens.Remove(img);
            }
        }

        var ordem = questao.Imagens.Count;
        foreach (var p in imagensNovas)
        {
            questao.Imagens.Add(NovaImagemDePendente(p, ordem++));
        }

        // Depois de todas as adições/remoções de imagem acima — reflete o
        // estado FINAL de Imagens, não o de antes da edição.
        await SincronizarTagDeImagemAsync(questao);

        // Precisa vir por último: compara contra o snapshot original de TODAS
        // as mutações acima (campos comuns + campos/coleções do subtipo).
        await RegistrarHistoricoDeEdicaoAsync(questao, meuId);

        await db.SaveChangesAsync();
    }

    public async Task AlternarAtivaAsync(Questao q, string? meuId)
    {
        if (q.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException("Você só pode ativar/desativar questões que você criou.");
        }

        q.Ativa = !q.Ativa;
        q.AtualizadoEm = DateTime.UtcNow;
        await RegistrarHistoricoDeEdicaoAsync(q, meuId);
        await db.SaveChangesAsync();
    }

    // --- Histórico / auditoria ---

    // Campos "principais" — os únicos que geram uma entrada com diff (ou pelo
    // menos com o nome do campo) no histórico. Todo o resto que puder mudar
    // numa edição (Origem/Ano/Referência, e os campos e coleções específicos
    // de cada tipo de questão — alternativas, pares, lacunas, gabarito) vira
    // uma única entrada genérica "Outros dados da questão" em vez de ser
    // listado aqui um por um: são formatos demais por tipo de questão pra
    // valer a pena detalhar, e o objetivo é auditoria ("o que mudou, quando,
    // quem"), não um diff completo.
    private static readonly HashSet<string> CamposPrincipaisHistorico = new()
    {
        nameof(Questao.Enunciado),
        nameof(Questao.Dificuldade),
        nameof(Questao.Bloom),
        nameof(Questao.Visibilidade),
        nameof(Questao.AssuntoId),
        nameof(Questao.Ativa),
    };

    // Compara o estado ATUAL da questão (já com as mutações da edição
    // aplicadas) contra o snapshot original que o EF Core guardou no momento
    // em que ela foi carregada do banco — é assim que sabemos o que realmente
    // mudou, sem precisar tirar uma cópia manual "antes" no início do método
    // de edição. Só funciona porque `questao` é uma entidade rastreada
    // (carregada via CarregarParaEdicaoAsync, não AsNoTracking). Chamado
    // tanto de AtualizarAsync quanto de AlternarAtivaAsync, sempre ANTES do
    // SaveChangesAsync que efetivamente grava a edição — as novas linhas de
    // histórico entram no mesmo SaveChanges, então nunca ficam dessincronizadas.
    private async Task RegistrarHistoricoDeEdicaoAsync(Questao questao, string? meuId)
    {
        db.ChangeTracker.DetectChanges();

        var entrada = db.Entry(questao);
        var agora = DateTime.UtcNow;
        var novasEntradas = new List<QuestaoHistorico>();

        void Adicionar(string campo, string? valorAnterior, string? valorNovo) =>
            novasEntradas.Add(new QuestaoHistorico
            {
                QuestaoId = questao.Id,
                UsuarioId = meuId,
                DataHora = agora,
                Campo = campo,
                ValorAnterior = valorAnterior,
                ValorNovo = valorNovo,
            });

        // Enunciado é texto longo demais pra virar uma linha "antes → depois"
        // legível — a entrada só marca QUE mudou, sem mostrar os valores.
        if (entrada.Property(nameof(Questao.Enunciado)).IsModified)
        {
            Adicionar("Enunciado", null, null);
        }

        if (entrada.Property(nameof(Questao.Dificuldade)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Dificuldade));
            Adicionar("Dificuldade", ((Dificuldade)prop.OriginalValue!).Rotulo(), ((Dificuldade)prop.CurrentValue!).Rotulo());
        }

        if (entrada.Property(nameof(Questao.Bloom)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Bloom));
            Adicionar(
                "Nível de Bloom",
                ((NivelBloom?)prop.OriginalValue)?.Rotulo() ?? "Sem classificação",
                ((NivelBloom?)prop.CurrentValue)?.Rotulo() ?? "Sem classificação");
        }

        if (entrada.Property(nameof(Questao.Visibilidade)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Visibilidade));
            Adicionar("Visibilidade", ((VisibilidadeQuestao)prop.OriginalValue!).Rotulo(), ((VisibilidadeQuestao)prop.CurrentValue!).Rotulo());
        }

        if (entrada.Property(nameof(Questao.Ativa)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Ativa));
            Adicionar("Status", (bool)prop.OriginalValue! ? "Ativa" : "Inativa", (bool)prop.CurrentValue! ? "Ativa" : "Inativa");
        }

        if (entrada.Property(nameof(Questao.AssuntoId)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.AssuntoId));
            var idAntigo = (int)prop.OriginalValue!;
            var idNovo = (int)prop.CurrentValue!;
            var nomes = await db.Assuntos
                .Where(a => a.Id == idAntigo || a.Id == idNovo)
                .ToDictionaryAsync(a => a.Id, a => a.Nome);
            Adicionar("Assunto", nomes.GetValueOrDefault(idAntigo, "?"), nomes.GetValueOrDefault(idNovo, "?"));
        }

        // Qualquer outro campo escalar que mudou (Origem/Ano/Referência, e —
        // por causa do TPT, ficam no MESMO EntityEntry — os campos
        // específicos do subtipo: RespostaEsperada, RespostaCorreta,
        // Tolerância, CriterioAvaliacao...) mais qualquer mudança nas
        // coleções específicas de tipo (Alternativas/Pares/Lacunas, que são
        // entidades à parte, não aparecem em `entrada.Properties`) — tudo
        // isso vira UMA entrada genérica, não um diff por campo.
        var outrosCamposMudaram = entrada.Properties
            .Any(p => p.IsModified && p.Metadata.Name != nameof(Questao.AtualizadoEm) && !CamposPrincipaisHistorico.Contains(p.Metadata.Name));

        var colecoesDeRespostaMudaram = db.ChangeTracker.Entries()
            .Any(e => e.State != EntityState.Unchanged && e.Entity is AlternativaQuestao or ParAssociacao or LacunaResposta);

        if (outrosCamposMudaram || colecoesDeRespostaMudaram)
        {
            Adicionar("Outros dados da questão", null, null);
        }

        if (novasEntradas.Count > 0)
        {
            db.QuestoesHistorico.AddRange(novasEntradas);
        }
    }

    // Só quem consegue VER a questão (mesma regra de VisivelPara) pode ver
    // seu histórico — do contrário a linha do histórico vazaria a existência/
    // metadados de questões privadas de outro professor pra quem nem
    // deveria saber que elas existem.
    public async Task<List<QuestaoHistorico>> ObterHistoricoAsync(int questaoId, string? meuId, int? minhaInstituicaoId)
    {
        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == questaoId);

        if (!visivel)
        {
            return new List<QuestaoHistorico>();
        }

        return await db.QuestoesHistorico
            .Include(h => h.Usuario)
            .Where(h => h.QuestaoId == questaoId)
            .OrderByDescending(h => h.DataHora)
            .ToListAsync();
    }

    // Log de auditoria "geral" (HistoricoQuestoes.razor) — mesma regra de
    // visibilidade do ObterHistoricoAsync (VisivelPara), só que aplicada a
    // TODAS as questões de uma vez via subquery de ids, em vez de checar uma
    // questão específica. Um professor só vê o histórico de edição do que ele
    // já enxergaria de qualquer forma na listagem de questões.
    public async Task<PaginaResultado<QuestaoHistorico>> ListarHistoricoAsync(
        HistoricoFiltro filtro, string? meuId, int? minhaInstituicaoId, int pagina, int tamanhoPagina)
    {
        var questoesVisiveis = db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId);

        if (filtro.DisciplinaId != 0)
        {
            questoesVisiveis = questoesVisiveis.Where(q => q.Assunto!.DisciplinaId == filtro.DisciplinaId);
        }

        if (filtro.AssuntoId != 0)
        {
            questoesVisiveis = questoesVisiveis.Where(q => q.AssuntoId == filtro.AssuntoId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            questoesVisiveis = questoesVisiveis.Where(q => EF.Functions.ILike(q.Enunciado, $"%{filtro.Texto}%"));
        }

        var idsVisiveis = questoesVisiveis.Select(q => q.Id);

        var query = db.QuestoesHistorico
            .Include(h => h.Usuario)
            .Include(h => h.Questao)
                .ThenInclude(q => q!.Assunto)
                    .ThenInclude(a => a!.Disciplina)
            .Where(h => idsVisiveis.Contains(h.QuestaoId));

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(h => h.DataHora)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<QuestaoHistorico> { Itens = itens, Total = total };
    }

    // Desativar (Ativa=false) é o caminho padrão pra "remover" uma questão sem
    // perder histórico. A exclusão física existe a pedido, mas é sempre
    // bloqueada pelo banco (FK Restrict) se a questão estiver vinculada a
    // alguma prova, então na prática só apaga de verdade quem nunca foi usado.
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
        // Duplicar é uma forma de "usar" a questão (vira uma cópia sua a
        // partir daqui) — mesma regra de quem PODE ENXERGAR a questão
        // original, não só quem criou.
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
        copia.Explicacao = completa.Explicacao;
        copia.Tags = completa.Tags.ToList();
        copia.CriadoPorId = userId;
        copia.CursoId = completa.CursoId;
        copia.ItensMatriz = completa.ItensMatriz.ToList();
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

    private static QuestaoImagem NovaImagemDePendente(PendenteImagem p, int ordem) => new()
    {
        NomeArquivo = p.NomeArquivo,
        ContentType = p.ContentType,
        Conteudo = p.Conteudo,
        Ordem = ordem,
        Legenda = string.IsNullOrWhiteSpace(p.Legenda) ? null : p.Legenda,
        TextoAlternativo = string.IsNullOrWhiteSpace(p.TextoAlternativo) ? null : p.TextoAlternativo,
        Alinhamento = p.Alinhamento,
        LarguraPercentual = p.LarguraPercentual,
    };

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

    // --- Alinhamento Curricular / ENADE ---
    //
    // A resolução/validação do vínculo Questão <-> Item de Matriz não mora
    // mais aqui (diferente do antigo ResolverDiretrizesAsync privado) — vive
    // em MatrizReferenciaService.ValidarItensDoCursoAsync, injetado como
    // `matrizService` acima, porque é a mesma regra de negócio que outras
    // telas (ex.: gerador de provas) também precisam.

    // Validações por tipo (mesma regra de sempre) + a checagem "defesa em
    // profundidade" de Institucional-sem-instituição — QuestaoInput.Enunciado
    // continua validado à parte pelo DataAnnotationsValidator do EditForm.
    private static void ValidarModelo(QuestaoInput modelo, int? minhaInstituicaoId)
    {
        if (modelo.AssuntoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione um assunto.");
        }

        // A opção "Institucional" já vem desabilitada no <select> quando o
        // professor não tem instituição definida, mas o valor pode chegar
        // aqui mesmo assim — sem essa checagem, a questão ficaria com
        // Visibilidade=Institucional sem instituição pra comparar.
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
