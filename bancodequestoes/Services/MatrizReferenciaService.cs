using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Dono da regra de negócio de Matriz de Referência: CRUD, transição de Status,
// diagnóstico e cobertura curricular; fachada fina sobre os Services de Item/vínculo.
public class MatrizReferenciaService(
    ApplicationDbContext db,
    MatrizAcessoService acesso,
    ItemMatrizReferenciaService itemService,
    ItemMatrizVinculoService vinculoService)
{
    // Mesmo one-liner replicado em ProvaService/QuestaoService pra achar a
    // Instituição do usuário logado.
    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    // --- Autorização (fachada fina sobre MatrizAcessoService) ---

    public Task<bool> TemAcessoAoCursoAsync(int cursoId, int? minhaInstituicaoId, bool ehAdmin) =>
        acesso.TemAcessoAoCursoAsync(cursoId, minhaInstituicaoId, ehAdmin);

    public Task<bool> TemAcessoDeLeituraAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin) =>
        acesso.TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin);

    // --- Matriz ---

    // AreaCursoId do Curso — usado pelas listagens "por curso" pra unir
    // também as matrizes ENADE/DCN da Área nacional que o Curso aponta.
    private Task<int?> ObterAreaCursoIdDoCursoAsync(int cursoId) =>
        db.Cursos.Where(c => c.Id == cursoId).Select(c => c.AreaCursoId).FirstOrDefaultAsync();

    // Query base reaproveitada por ListarPorCursoAsync/ListarAtivasPorCursoAsync/
    // ListarVinculaveisPorCursoAsync (só o filtro de Status muda).
    private async Task<IQueryable<MatrizReferencia>> QueryMatrizesDoCursoAsync(int cursoId)
    {
        var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId);
        return db.MatrizesReferencia.Where(m =>
            m.CursoId == cursoId || (areaCursoId != null && m.AreaCursoId == areaCursoId));
    }

    // Lista TODAS as matrizes do curso — quem decide o que mostrar é a tela.
    // Mais recentes primeiro dentro do mesmo Tipo.
    public async Task<List<MatrizReferencia>> ListarPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
                .ThenByDescending(m => m.CriadoEm)
            .ToListAsync();
    }

    // Usado pelo gerador de provas (mostra só matrizes ATIVAS por padrão).
    public async Task<List<MatrizReferencia>> ListarAtivasPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .Where(m => m.Status == StatusMatrizReferencia.Ativa)
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
            .ToListAsync();
    }

    // Matrizes que o professor pode escolher pra classificar uma questão:
    // Ativa e Historica (não Rascunho).
    public async Task<List<MatrizReferencia>> ListarVinculaveisPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .Where(m => m.Status != StatusMatrizReferencia.Rascunho)
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
            .ToListAsync();
    }

    // --- Item de Matriz e Vínculo Questão<->Item (fachada fina) ---

    public Task<List<ItemMatrizReferencia>> ListarItensVinculaveisPorCursoAsync(int cursoId) =>
        vinculoService.ListarItensVinculaveisPorCursoAsync(cursoId);

    public Task<List<ItemMatrizReferencia>> ListarItensVinculaveisPorAreasCursoAsync(IEnumerable<int> areaCursoIds) =>
        vinculoService.ListarItensVinculaveisPorAreasCursoAsync(areaCursoIds);

    public Task<List<ItemMatrizReferencia>> ListarItensEnadeAtivosAsync() =>
        vinculoService.ListarItensEnadeAtivosAsync();

    public Task<List<ItemMatrizReferencia>> ValidarItensPorAreasCursoAsync(IEnumerable<int> areaCursoIds, IEnumerable<int> itemIds) =>
        vinculoService.ValidarItensPorAreasCursoAsync(areaCursoIds, itemIds);

    public Task<List<ItemMatrizReferencia>> ValidarItensDoCursoAsync(int? cursoId, IEnumerable<int> itemIds) =>
        vinculoService.ValidarItensDoCursoAsync(cursoId, itemIds);

    public Task<List<ItemMatrizReferencia>> ListarItensAsync(int matrizId) =>
        itemService.ListarItensAsync(matrizId);

    public Task<ItemMatrizReferencia?> ObterItemAsync(int id) =>
        itemService.ObterItemAsync(id);

    public Task<ItemMatrizReferencia> CriarItemAsync(int matrizId, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin) =>
        itemService.CriarItemAsync(matrizId, modelo, minhaInstituicaoId, ehAdmin);

    public Task AtualizarItemAsync(int id, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin) =>
        itemService.AtualizarItemAsync(id, modelo, minhaInstituicaoId, ehAdmin);

    public Task AlternarAtivoItemAsync(int id, int? minhaInstituicaoId, bool ehAdmin) =>
        itemService.AlternarAtivoItemAsync(id, minhaInstituicaoId, ehAdmin);

    public Task ExcluirItemAsync(ItemMatrizReferencia item, int? minhaInstituicaoId, bool ehAdmin) =>
        itemService.ExcluirItemAsync(item, minhaInstituicaoId, ehAdmin);

    public Task<MatrizReferencia?> ObterAsync(int id) =>
        db.MatrizesReferencia.Include(m => m.Curso).Include(m => m.AreaCurso).FirstOrDefaultAsync(m => m.Id == id);

    // Sobrecarga autorizada — devolve null pra id inexistente OU sem acesso
    // de leitura, sem distinguir os dois (não vaza existência de matriz alheia).
    public async Task<MatrizReferencia?> ObterAsync(int id, int? minhaInstituicaoId, bool ehAdmin)
    {
        var matriz = await ObterAsync(id);
        if (matriz is null)
        {
            return null;
        }
        return await acesso.TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin) ? matriz : null;
    }

    // Traz os itens junto — usado por MatrizItensPage.razor e pelo dashboard
    // de cobertura.
    public Task<MatrizReferencia?> ObterComItensAsync(int id) =>
        db.MatrizesReferencia
            .Include(m => m.Curso)
            .Include(m => m.AreaCurso)
            .Include(m => m.Itens)
            .FirstOrDefaultAsync(m => m.Id == id);

    // Sobrecarga autorizada — mesmo espírito de ObterAsync acima.
    public async Task<MatrizReferencia?> ObterComItensAsync(int id, int? minhaInstituicaoId, bool ehAdmin)
    {
        var matriz = await ObterComItensAsync(id);
        if (matriz is null)
        {
            return null;
        }
        return await acesso.TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin) ? matriz : null;
    }

    public async Task<MatrizReferencia> CriarAsync(MatrizReferenciaInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        ValidarMatriz(modelo);

        var nacional = modelo.Tipo.EhEscopoNacional();
        if (nacional)
        {
            MatrizAcessoService.GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await acesso.GarantirAcessoAoCursoAsync(modelo.CursoId, minhaInstituicaoId, ehAdmin);
        }

        var matriz = new MatrizReferencia
        {
            CursoId = nacional ? null : modelo.CursoId,
            AreaCursoId = nacional ? modelo.AreaCursoId : null,
            Nome = modelo.Nome,
            Tipo = modelo.Tipo,
            Ano = modelo.Ano,
            Edicao = string.IsNullOrWhiteSpace(modelo.Edicao) ? null : modelo.Edicao,
            Orgao = string.IsNullOrWhiteSpace(modelo.Orgao) ? null : modelo.Orgao,
            Documento = string.IsNullOrWhiteSpace(modelo.Documento) ? null : modelo.Documento,
            UrlFonte = string.IsNullOrWhiteSpace(modelo.UrlFonte) ? null : modelo.UrlFonte,
            Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao,
            Status = modelo.Status,
        };

        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();

        // Matriz ENADE já nascendo Ativa também precisa desativar outras
        // ativas do mesmo escopo.
        if (matriz.Tipo == TipoMatrizReferencia.ENADE && matriz.Status == StatusMatrizReferencia.Ativa)
        {
            await DesativarOutrasEnadeAtivasAsync(matriz);
        }

        return matriz;
    }

    public async Task AtualizarAsync(int id, MatrizReferenciaInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        ValidarMatriz(modelo);

        var matriz = await db.MatrizesReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        // Autoriza pelo escopo da matriz JÁ EXISTENTE — CursoId/AreaCursoId
        // não são reatribuíveis livremente (ver abaixo).
        await acesso.GarantirAcessoEscritaMatrizAsync(matriz, minhaInstituicaoId, ehAdmin);

        // Trocar de escopo (Curso <-> Área) na edição não é permitido — deixaria
        // vínculos inconsistentes; crie uma matriz nova. Nacional é decidido pelo Tipo, nunca por AreaCursoId.
        var novoEscopoNacional = modelo.Tipo.EhEscopoNacional();
        var escopoAtualNacional = matriz.Tipo.EhEscopoNacional();
        if (novoEscopoNacional != escopoAtualNacional)
        {
            throw new OperacaoInvalidaException(
                $"Não é possível mudar o Tipo de \"{matriz.Nome}\" entre um tipo de escopo nacional (ENADE/DCN, ligado a uma Área de Curso) e um de escopo institucional (PPC/Institucional/Outro, ligado a um Curso) — crie uma matriz nova no escopo certo em vez disso.");
        }

        matriz.Nome = modelo.Nome;
        matriz.Tipo = modelo.Tipo;
        matriz.Ano = modelo.Ano;
        matriz.Edicao = string.IsNullOrWhiteSpace(modelo.Edicao) ? null : modelo.Edicao;
        matriz.Orgao = string.IsNullOrWhiteSpace(modelo.Orgao) ? null : modelo.Orgao;
        matriz.Documento = string.IsNullOrWhiteSpace(modelo.Documento) ? null : modelo.Documento;
        matriz.UrlFonte = string.IsNullOrWhiteSpace(modelo.UrlFonte) ? null : modelo.UrlFonte;
        matriz.Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao;
        matriz.Status = modelo.Status;

        // CursoId/AreaCursoId só mudam quando a matriz ainda não tem item —
        // sem vínculo nenhum, não há risco de inconsistência.
        var temItens = await db.ItensMatrizReferencia.AnyAsync(i => i.MatrizReferenciaId == matriz.Id);
        if (!temItens)
        {
            matriz.CursoId = novoEscopoNacional ? null : modelo.CursoId;
            matriz.AreaCursoId = novoEscopoNacional ? modelo.AreaCursoId : null;
        }

        // Virando ENADE+Ativa, desativa qualquer outra ENADE Ativa do mesmo
        // escopo (mesma transação); PPC/Institucional/Outro não entram nessa regra.
        if (matriz.Tipo == TipoMatrizReferencia.ENADE && matriz.Status == StatusMatrizReferencia.Ativa)
        {
            await DesativarOutrasEnadeAtivasAsync(matriz);
            return;
        }

        await db.SaveChangesAsync();
    }

    // Desativa (vira Histórica) qualquer outra matriz ENADE Ativa do MESMO
    // escopo — garante uma ativa por vez, tudo na mesma transação.
    private async Task DesativarOutrasEnadeAtivasAsync(MatrizReferencia matriz)
    {
        using var transacao = await db.Database.BeginTransactionAsync();

        var query = db.MatrizesReferencia.Where(m =>
            m.Tipo == TipoMatrizReferencia.ENADE
            && m.Status == StatusMatrizReferencia.Ativa
            && m.Id != matriz.Id);

        query = matriz.AreaCursoId is not null
            ? query.Where(m => m.AreaCursoId == matriz.AreaCursoId)
            : query.Where(m => m.CursoId == matriz.CursoId);

        var outrasAtivas = await query.ToListAsync();

        foreach (var outra in outrasAtivas)
        {
            outra.Status = StatusMatrizReferencia.Historica;
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    private static void ValidarMatriz(MatrizReferenciaInput modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo.Nome))
        {
            throw new OperacaoInvalidaException("Informe um nome para a matriz (ex.: \"ENADE 2023\").");
        }

        if (modelo.Tipo.EhEscopoNacional())
        {
            if (modelo.AreaCursoId is null or 0)
            {
                throw new OperacaoInvalidaException(
                    "Selecione uma Área de Curso — matrizes ENADE/DCN são documentos nacionais do MEC/INEP, não presas ao curso de uma instituição específica.");
            }
        }
        else if (modelo.CursoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione um curso.");
        }
    }

    // Excluir só funciona sem itens/vínculos; o caminho normal é mudar Status
    // pra Historica. Checa ANTES de remover pois o erro do banco varia por caso.
    public async Task ExcluirAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        await acesso.GarantirAcessoEscritaMatrizAsync(matriz, minhaInstituicaoId, ehAdmin);

        var temItens = await db.ItensMatrizReferencia.AnyAsync(i => i.MatrizReferenciaId == matriz.Id);
        if (temItens)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{matriz.Nome}\": ela ainda tem itens cadastrados. Considere mudar o status para Histórica em vez de excluir.");
        }

        try
        {
            db.MatrizesReferencia.Remove(matriz);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Rede de segurança pra corrida entre a checagem e o SaveChangesAsync.
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{matriz.Nome}\": ela ainda tem itens ou vínculos cadastrados. Considere mudar o status para Histórica em vez de excluir.");
        }
    }

    // --- Importação CSV/XLSX/JSON (ver Importacao/ItemMatrizImportParser.cs) ---

    // Aplica o resultado já revisado da pré-visualização — UPSERT por
    // Código (reimportar atualiza em vez de duplicar).
    public async Task<int> ImportarItensAsync(int matrizId, List<ItemMatrizImportado> linhas, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await db.MatrizesReferencia.AnyAsync(m => m.Id == matrizId))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        await acesso.GarantirAcessoAMatrizAsync(matrizId, minhaInstituicaoId, ehAdmin);

        // Transação: ou importa tudo, ou nada muda.
        using var transacao = await db.Database.BeginTransactionAsync();
        var importados = await ImportarItensSemTransacaoAsync(matrizId, linhas);
        await transacao.CommitAsync();
        return importados;
    }

    // Núcleo do upsert, sem transação própria — usado por ImportarItensAsync
    // e por ImportarMatrizCompletaAsync (que precisam da MESMA transação).
    private async Task<int> ImportarItensSemTransacaoAsync(int matrizId, List<ItemMatrizImportado> linhas)
    {
        var selecionadas = linhas.Where(l => l.Selecionada).ToList();
        if (selecionadas.Count == 0)
        {
            return 0;
        }

        var existentes = await db.ItensMatrizReferencia
            .Where(i => i.MatrizReferenciaId == matrizId)
            .ToListAsync();

        var porCodigo = existentes.ToDictionary(i => i.Codigo, i => i, StringComparer.OrdinalIgnoreCase);

        foreach (var linha in selecionadas)
        {
            if (porCodigo.TryGetValue(linha.Codigo, out var existente))
            {
                existente.Titulo = linha.Titulo;
                existente.Descricao = linha.Descricao;
                existente.Tipo = linha.Tipo;
                existente.Ordem = linha.Ordem;
            }
            else
            {
                var novo = new ItemMatrizReferencia
                {
                    MatrizReferenciaId = matrizId,
                    Codigo = linha.Codigo,
                    Titulo = linha.Titulo,
                    Descricao = linha.Descricao,
                    Tipo = linha.Tipo,
                    Ordem = linha.Ordem,
                };
                db.ItensMatrizReferencia.Add(novo);
                porCodigo[linha.Codigo] = novo;
            }
        }

        await db.SaveChangesAsync();
        return selecionadas.Count;
    }

    // --- Diagnóstico pré-geração ---

    // Quantas questões ativas/visíveis já vinculadas a cada item — pra tela
    // mostrar disponibilidade antes de gerar a prova.
    public async Task<Dictionary<int, int>> ContarQuestoesDisponiveisPorItemAsync(
        IEnumerable<int> itemIds, string? meuId, int? minhaInstituicaoId)
    {
        var ids = itemIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        // Reaproveita QuestaoVisibilidade.VisivelPara em vez de reescrever a
        // expressão Compartilhada/CriadoPorId/Institucional aqui dentro.
        return await db.QuestoesItensMatriz
            .Where(qi => ids.Contains(qi.ItemMatrizReferenciaId))
            .Where(qi => db.Questoes.AsQueryable().VisivelPara(meuId, minhaInstituicaoId)
                .Any(q => q.Id == qi.QuestaoId && q.Ativa))
            .GroupBy(qi => qi.ItemMatrizReferenciaId)
            .Select(g => new { ItemId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantidade);
    }

    // Questões DISTINTAS que atendem a pelo menos um item do conjunto —
    // nunca soma simples (superestimaria quando itens compartilham questão).
    public async Task<int> ContarQuestoesDistintasDisponiveisAsync(
        IEnumerable<int> itemIds, string? meuId, int? minhaInstituicaoId)
    {
        var ids = itemIds.ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        return await db.QuestoesItensMatriz
            .Where(qi => ids.Contains(qi.ItemMatrizReferenciaId))
            .Where(qi => db.Questoes.AsQueryable().VisivelPara(meuId, minhaInstituicaoId)
                .Any(q => q.Id == qi.QuestaoId && q.Ativa))
            .Select(qi => qi.QuestaoId)
            .Distinct()
            .CountAsync();
    }

    // --- Dashboard: Cobertura Curricular (escopado a UMA matriz) ---

    public async Task<CoberturaCurricularResultado> ObterCoberturaCurricularAsync(
        int matrizId, string? meuId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var matriz = await db.MatrizesReferencia
            .Include(m => m.Curso)
            .Include(m => m.AreaCurso)
            .FirstOrDefaultAsync(m => m.Id == matrizId)
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        // Dashboard institucional só pra quem tem relação com o curso — sem
        // isso vazaria contagem de questões de outra instituição.
        if (!await acesso.TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        var itens = await ListarItensAsync(matrizId);

        // Denominador da cobertura: matriz nacional conta toda questão aplicável
        // à Área (QuestaoAreaCurso); institucional conta só quem tem item vinculado A ESTA matriz.
        var totalQuestoes = matriz.CursoId is not null
            ? await db.Questoes.CountAsync(q => q.Ativa && q.ItensMatriz.Any(i => i.MatrizReferenciaId == matrizId))
            : await db.Questoes.CountAsync(q => q.Ativa && q.AreasCurso.Any(a => a.Id == matriz.AreaCursoId));

        var totalQuestoesMinhas = matriz.CursoId is not null
            ? await db.Questoes.AsQueryable()
                .VisivelPara(meuId, minhaInstituicaoId)
                .CountAsync(q => q.Ativa && q.ItensMatriz.Any(i => i.MatrizReferenciaId == matrizId))
            : await db.Questoes.AsQueryable()
                .VisivelPara(meuId, minhaInstituicaoId)
                .CountAsync(q => q.Ativa && q.AreasCurso.Any(a => a.Id == matriz.AreaCursoId));

        var contagemPorItem = await db.QuestoesItensMatriz
            .Where(qi => qi.ItemMatrizReferencia!.MatrizReferenciaId == matrizId && qi.Questao!.Ativa)
            .GroupBy(qi => qi.ItemMatrizReferenciaId)
            .Select(g => new { ItemId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantidade);

        var grupos = itens
            .GroupBy(i => i.Tipo)
            .OrderBy(g => g.Key)
            .Select(g => new GrupoCobertura
            {
                Tipo = g.Key,
                Linhas = g
                    .Select(i => new LinhaCoberturaItem { Item = i, Quantidade = contagemPorItem.GetValueOrDefault(i.Id) })
                    .ToList(),
            })
            .ToList();

        var crossTab = await db.QuestoesItensMatriz
            .Where(qi => qi.ItemMatrizReferencia!.MatrizReferenciaId == matrizId && qi.Questao!.Ativa)
            .GroupBy(qi => new
            {
                qi.ItemMatrizReferenciaId,
                DisciplinaId = qi.Questao!.Assunto!.DisciplinaId,
                DisciplinaNome = qi.Questao.Assunto!.Disciplina!.Nome,
            })
            .Select(g => new CelulaCrossTabDisciplina
            {
                ItemMatrizReferenciaId = g.Key.ItemMatrizReferenciaId,
                DisciplinaId = g.Key.DisciplinaId,
                DisciplinaNome = g.Key.DisciplinaNome,
                Quantidade = g.Count(),
            })
            .ToListAsync();

        return new CoberturaCurricularResultado
        {
            Matriz = matriz,
            TotalQuestoesDoCurso = totalQuestoes,
            TotalQuestoesMinhasProvas = totalQuestoesMinhas,
            Grupos = grupos,
            CrossTabDisciplinas = crossTab,
        };
    }

    // Importação de MATRIZ COMPLETA via JSON, em duas etapas: PrepararImportacaoMatrizCompletaAsync
    // monta prévia sem tocar no banco; ImportarMatrizCompletaAsync só roda após confirmação do curso/tipo.

    // Acha Curso pelo NOME exato, só dentro do que o usuário acessa — um
    // JSON malicioso não "encontra" curso de outra instituição.
    private async Task<Curso?> LocalizarCursoAsync(string? nomeCurso, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (string.IsNullOrWhiteSpace(nomeCurso))
        {
            return null;
        }

        var query = db.Cursos.Include(c => c.Instituicao).AsQueryable();
        if (!ehAdmin)
        {
            query = query.Where(c => c.InstituicaoId == minhaInstituicaoId);
        }

        return await query.FirstOrDefaultAsync(c => EF.Functions.ILike(c.Nome, nomeCurso.Trim()));
    }

    // Mesmo espírito de LocalizarCursoAsync, pra Área de Curso — sem filtro
    // de instituição (AreaCurso é catálogo nacional).
    private async Task<AreaCurso?> LocalizarAreaCursoAsync(string? nomeArea)
    {
        if (string.IsNullOrWhiteSpace(nomeArea))
        {
            return null;
        }

        return await db.AreasCurso.FirstOrDefaultAsync(a => EF.Functions.ILike(a.Nome, nomeArea.Trim()));
    }

    // Aceita nome do enum ou rótulo em português; devolve null (não "Outro")
    // porque o professor sempre confirma o Tipo na prévia antes de importar.
    private static TipoMatrizReferencia? ResolverTipoMatriz(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        if (Enum.TryParse<TipoMatrizReferencia>(texto.Trim(), ignoreCase: true, out var porNome))
        {
            return porNome;
        }

        var normalizado = texto.Trim().ToLowerInvariant();
        return Enum.GetValues<TipoMatrizReferencia>()
            .Cast<TipoMatrizReferencia?>()
            .FirstOrDefault(t => t!.Value.Rotulo().ToLowerInvariant() == normalizado);
    }

    public async Task<PreviaImportacaoMatrizCompleta> PrepararImportacaoMatrizCompletaAsync(
        MatrizCompletaImportada dados, int? minhaInstituicaoId, bool ehAdmin)
    {
        // Tenta achar o texto "curso" do JSON tanto como Curso quanto como
        // Área — qual dos dois vale depende do Tipo resolvido.
        var curso = await LocalizarCursoAsync(dados.Curso, minhaInstituicaoId, ehAdmin);
        var area = await LocalizarAreaCursoAsync(dados.Curso);
        var tipo = ResolverTipoMatriz(dados.Tipo);

        // "Matriz parecida": mesmo escopo + tipo + ano — só dá pra checar
        // com Tipo e escopo já resolvidos.
        MatrizReferencia? similar = tipo is null
            ? null
            : tipo.Value.EhEscopoNacional()
                ? (area is null ? null : await db.MatrizesReferencia.FirstOrDefaultAsync(m => m.AreaCursoId == area.Id && m.Tipo == tipo.Value && m.Ano == dados.Ano))
                : (curso is null ? null : await db.MatrizesReferencia.FirstOrDefaultAsync(m => m.CursoId == curso.Id && m.Tipo == tipo.Value && m.Ano == dados.Ano));

        var contagemPorTipo = dados.Itens.Itens
            .GroupBy(i => i.Tipo)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PreviaImportacaoMatrizCompleta
        {
            CursoTexto = dados.Curso,
            CursoEncontrado = curso,
            AreaCursoEncontrada = area,
            TipoTexto = dados.Tipo,
            Tipo = tipo,
            Ano = dados.Ano,
            Edicao = dados.Edicao,
            Orgao = dados.Orgao,
            Documento = dados.Documento,
            UrlFonte = dados.UrlFonte,
            Descricao = dados.Descricao,
            ContagemPorTipo = contagemPorTipo,
            Total = dados.Itens.Itens.Count,
            MatrizSimilarExistente = similar,
            Erros = dados.Itens.Erros,
        };
    }

    // Cria a matriz (sempre Rascunho) e importa os itens na mesma transação;
    // cursoId/areaCursoId vêm do seletor, podendo divergir do sugerido pelo parser.
    public async Task<(MatrizReferencia Matriz, int ItensImportados)> ImportarMatrizCompletaAsync(
        MatrizCompletaImportada dados,
        int? cursoId,
        int? areaCursoId,
        TipoMatrizReferencia tipo,
        bool confirmarApesarDeSimilar,
        int? minhaInstituicaoId,
        bool ehAdmin)
    {
        var nacional = tipo.EhEscopoNacional();

        // Nunca confia só na UI — revalida acesso ao escopo escolhido.
        if (nacional)
        {
            if (areaCursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione a Área de Curso correspondente.");
            }
            MatrizAcessoService.GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            if (cursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione o curso correspondente.");
            }
            await acesso.GarantirAcessoAoCursoAsync(cursoId.Value, minhaInstituicaoId, ehAdmin);
        }

        if (dados.Itens.Itens.Count == 0)
        {
            throw new OperacaoInvalidaException("Nenhum item reconhecido no arquivo — nada para importar.");
        }

        if (!confirmarApesarDeSimilar)
        {
            var similar = nacional
                ? await db.MatrizesReferencia.FirstOrDefaultAsync(m => m.AreaCursoId == areaCursoId && m.Tipo == tipo && m.Ano == dados.Ano)
                : await db.MatrizesReferencia.FirstOrDefaultAsync(m => m.CursoId == cursoId && m.Tipo == tipo && m.Ano == dados.Ano);
            if (similar is not null)
            {
                throw new OperacaoInvalidaException(
                    $"Já existe uma matriz \"{similar.Nome}\" ({tipo.Rotulo()}{(dados.Ano is int a ? $" {a}" : "")}) ness{(nacional ? "a área de curso" : "e curso")}. Confirme a importação como nova matriz se isso for intencional.");
            }
        }

        using var transacao = await db.Database.BeginTransactionAsync();

        var matriz = new MatrizReferencia
        {
            CursoId = nacional ? null : cursoId,
            AreaCursoId = nacional ? areaCursoId : null,
            Nome = MontarNomeSugerido(tipo, dados.Ano, dados.Edicao),
            Tipo = tipo,
            Ano = dados.Ano,
            Edicao = dados.Edicao,
            Orgao = dados.Orgao,
            Documento = dados.Documento,
            UrlFonte = dados.UrlFonte,
            Descricao = dados.Descricao,
            Status = StatusMatrizReferencia.Rascunho,
        };
        db.MatrizesReferencia.Add(matriz);
        await db.SaveChangesAsync();

        var itensImportados = await ImportarItensSemTransacaoAsync(matriz.Id, dados.Itens.Itens);

        await transacao.CommitAsync();
        return (matriz, itensImportados);
    }

    private static string MontarNomeSugerido(TipoMatrizReferencia tipo, int? ano, string? edicao)
    {
        var partes = new List<string> { tipo.Rotulo() };
        if (ano is int a)
        {
            partes.Add(a.ToString());
        }
        else if (!string.IsNullOrWhiteSpace(edicao))
        {
            partes.Add(edicao);
        }

        return string.Join(" ", partes);
    }
}

// Prévia da importação de matriz completa — o que a tela mostra ANTES de
// qualquer escrita no banco.
public sealed class PreviaImportacaoMatrizCompleta
{
    public required string? CursoTexto { get; init; }
    public required Curso? CursoEncontrado { get; init; }
    // Preenchida quando o texto do JSON bate com uma Área cadastrada (só
    // relevante se Tipo é ENADE/DCN) — quem decide qual vale é o Tipo.
    public required AreaCurso? AreaCursoEncontrada { get; init; }
    public required string? TipoTexto { get; init; }
    public required TipoMatrizReferencia? Tipo { get; init; }
    public required int? Ano { get; init; }
    public required string? Edicao { get; init; }
    public required string? Orgao { get; init; }
    public required string? Documento { get; init; }
    public required string? UrlFonte { get; init; }
    public required string? Descricao { get; init; }
    public required Dictionary<TipoItemMatriz, int> ContagemPorTipo { get; init; }
    public required int Total { get; init; }
    public required MatrizReferencia? MatrizSimilarExistente { get; init; }
    public required List<string> Erros { get; init; }
}
