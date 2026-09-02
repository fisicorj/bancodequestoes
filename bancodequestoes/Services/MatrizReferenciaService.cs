using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Dono de toda a regra de negócio de Matriz de Referência Curricular / ENADE:
// CRUD de MatrizReferencia e de ItemMatrizReferencia, transição de Status
// (Rascunho/Ativa/Historica), validação de vínculo Questão<->Item (nunca
// confiando só no id vindo do Blazor), diagnóstico de disponibilidade e o
// dashboard de Cobertura Curricular. Fica num Service à parte de
// CursoService de propósito — ver comentário em CursoService — mas continua
// seguindo o mesmo padrão de todos os outros: Service = regra/consulta,
// .razor = só UI/binding.
public class MatrizReferenciaService(ApplicationDbContext db)
{
    // Mesmo one-liner já duplicado em ProvaService/QuestaoService (padrão já
    // existente no projeto pra achar a Instituição do usuário logado a partir
    // do id do Identity) — replicado aqui em vez de criar uma dependência
    // cruzada entre Services só por causa de uma linha.
    public Task<int?> ObterInstituicaoDoUsuarioAsync(string? userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

    // --- Autorização (itens 8/9/10 da 2ª rodada de revisão) ---
    //
    // Antes desta rodada, NENHUM método deste Service validava se o usuário
    // tinha relação com o Curso da matriz — bastava estar autenticado
    // ([Authorize] na página) pra ler/editar/excluir/importar itens de
    // qualquer matriz de qualquer instituição, só sabendo o id (IDOR).
    //
    // Não existe hoje, no projeto, um controle de acesso por
    // Instituição/Curso pronto pra "reaproveitar" (Curso/Instituicao/Turma
    // são, antes e depois desta rodada, globalmente legíveis/editáveis por
    // qualquer usuário autenticado — GAP PRÉ-EXISTENTE e fora do escopo deste
    // pedido, documentado no relatório final). O único padrão de escopo que
    // já existe no projeto é o de Questao (QuestaoVisibilidade.VisivelPara,
    // comparando ApplicationUser.InstituicaoId com a Instituição de quem
    // criou a questão). Replicamos aqui o MESMO princípio — comparar
    // ApplicationUser.InstituicaoId (de quem está pedindo) com
    // Curso.InstituicaoId (dono da matriz) — só para as telas de Matriz de
    // Referência, sem tocar em CursoService/InstituicaoService.
    //
    // Admin tem bypass total (mesmo padrão já usado em
    // Admin/UsuariosList.razor, [Authorize(Roles = "Admin")]).
    public async Task<bool> TemAcessoAoCursoAsync(int cursoId, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (ehAdmin)
        {
            return true;
        }

        if (minhaInstituicaoId is null)
        {
            return false;
        }

        var instituicaoDoCurso = await db.Cursos
            .Where(c => c.Id == cursoId)
            .Select(c => (int?)c.InstituicaoId)
            .FirstOrDefaultAsync();

        return instituicaoDoCurso is not null && instituicaoDoCurso == minhaInstituicaoId;
    }

    // Mesma checagem acima, mas lança em vez de devolver bool — usado nos
    // métodos de escrita (criar/editar/excluir/importar), que já seguem o
    // padrão de comunicar erro de negócio via OperacaoInvalidaException
    // (capturada nas telas). Mensagem propositalmente igual à de "não
    // encontrada" — não confirmamos pra quem tenta um id de outra instituição
    // que aquele curso/matriz/item "existe" (mesmo espírito de não vazar
    // informação de um 404 comum).
    private async Task GarantirAcessoAoCursoAsync(int cursoId, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await TemAcessoAoCursoAsync(cursoId, minhaInstituicaoId, ehAdmin))
        {
            throw new OperacaoInvalidaException("Curso não encontrado.");
        }
    }

    // --- Autorização de matrizes de escopo NACIONAL (Área de Curso — ver
    // AreaCurso e o comentário de escopo duplo em MatrizReferencia) ---
    //
    // LEITURA de uma matriz ENADE/DCN é aberta a qualquer usuário autenticado
    // (quem chama já passou pelo [Authorize] da página): ela não pertence a
    // uma instituição, então não existe "instituição dona" pra comparar, e o
    // ponto inteiro de ser nacional é ser vista por todo mundo que a usa.
    // ESCRITA (criar/editar/excluir/importar itens) fica restrita a Admin —
    // sem isso, qualquer professor de qualquer instituição poderia alterar
    // uma matriz nacional compartilhada por todas as outras.
    private static void GarantirAcessoEscritaArea(bool ehAdmin)
    {
        if (!ehAdmin)
        {
            throw new OperacaoInvalidaException(
                "Apenas administradores podem gerenciar matrizes de escopo nacional (ENADE/DCN) — elas são compartilhadas entre instituições.");
        }
    }

    // Leitura de uma matriz JÁ CARREGADA — usada pelas sobrecargas
    // autorizadas de ObterAsync/ObterComItensAsync abaixo.
    public async Task<bool> TemAcessoDeLeituraAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (matriz.AreaCursoId is not null)
        {
            return true;
        }

        return matriz.CursoId is int cursoId && await TemAcessoAoCursoAsync(cursoId, minhaInstituicaoId, ehAdmin);
    }

    // Escrita numa matriz JÁ CARREGADA — usada por AtualizarAsync/
    // ExcluirAsync. Curso-escopada segue a regra de sempre (mesma
    // instituição, ou Admin); Área-escopada exige Admin (ver
    // GarantirAcessoEscritaArea acima).
    private async Task GarantirAcessoEscritaMatrizAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (matriz.AreaCursoId is not null)
        {
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await GarantirAcessoAoCursoAsync(matriz.CursoId!.Value, minhaInstituicaoId, ehAdmin);
        }
    }

    // Escopo (CursoId XOR AreaCursoId) de uma matriz ou item, buscado só com
    // as duas colunas — usado por GarantirAcessoAMatrizAsync/
    // GarantirAcessoAoItemAsync abaixo (telas de gestão de itens/importação,
    // que não têm a MatrizReferencia inteira já carregada).
    private async Task<(int? CursoId, int? AreaCursoId)> ObterEscopoDaMatrizOuFalharAsync(int matrizId)
    {
        var escopo = await db.MatrizesReferencia
            .Where(m => m.Id == matrizId)
            .Select(m => new { m.CursoId, m.AreaCursoId })
            .FirstOrDefaultAsync()
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        return (escopo.CursoId, escopo.AreaCursoId);
    }

    private async Task<(int? CursoId, int? AreaCursoId)> ObterEscopoDoItemOuFalharAsync(int itemId)
    {
        var escopo = await db.ItensMatrizReferencia
            .Where(i => i.Id == itemId)
            .Select(i => new { i.MatrizReferencia!.CursoId, i.MatrizReferencia!.AreaCursoId })
            .FirstOrDefaultAsync()
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        return (escopo.CursoId, escopo.AreaCursoId);
    }

    private async Task GarantirAcessoEscritaEscopoAsync((int? CursoId, int? AreaCursoId) escopo, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (escopo.AreaCursoId is not null)
        {
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await GarantirAcessoAoCursoAsync(escopo.CursoId!.Value, minhaInstituicaoId, ehAdmin);
        }
    }

    private async Task GarantirAcessoAMatrizAsync(int matrizId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var escopo = await ObterEscopoDaMatrizOuFalharAsync(matrizId);
        await GarantirAcessoEscritaEscopoAsync(escopo, minhaInstituicaoId, ehAdmin);
    }

    private async Task GarantirAcessoAoItemAsync(int itemId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var escopo = await ObterEscopoDoItemOuFalharAsync(itemId);
        await GarantirAcessoEscritaEscopoAsync(escopo, minhaInstituicaoId, ehAdmin);
    }

    // --- Matriz ---

    // AreaCursoId do Curso informado, se houver — usado por TODAS as
    // listagens "por curso" abaixo pra também trazer, UNIDAS (union), as
    // matrizes ENADE/DCN cadastradas na Área de Curso nacional que esse Curso
    // aponta (ver Curso.AreaCursoId / MatrizReferencia escopo duplo). Sem
    // AreaCursoId definido no Curso, o union não traz nada extra — só as
    // matrizes do próprio Curso, comportamento idêntico ao de antes desta
    // rodada.
    private Task<int?> ObterAreaCursoIdDoCursoAsync(int cursoId) =>
        db.Cursos.Where(c => c.Id == cursoId).Select(c => c.AreaCursoId).FirstOrDefaultAsync();

    // Query base reaproveitada por ListarPorCursoAsync/ListarAtivasPorCursoAsync/
    // ListarVinculaveisPorCursoAsync — as três SÓ diferem no filtro de Status
    // aplicado por cima. Assinatura de cada uma continua recebendo só
    // cursoId (nada muda nas telas que já chamam essas três — ProvaForm,
    // QuestaoForm, GeradorAutomatico, CursoForm — ver comentário em
    // MatrizReferencia sobre o design "zero call-sites pra mudar").
    private async Task<IQueryable<MatrizReferencia>> QueryMatrizesDoCursoAsync(int cursoId)
    {
        var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId);
        return db.MatrizesReferencia.Where(m =>
            m.CursoId == cursoId || (areaCursoId != null && m.AreaCursoId == areaCursoId));
    }

    // Lista TODAS as matrizes do curso (qualquer Status) — quem decide o que
    // mostrar/esconder por padrão é a tela (ex.: MatrizesPage.razor mostra
    // todas; o seletor do gerador mostra só Ativa por padrão — ver
    // ListarAtivasPorCursoAsync). Mais recentes primeiro dentro do mesmo Tipo,
    // pra "ENADE 2026" aparecer antes de "ENADE 2023" na lista.
    public async Task<List<MatrizReferencia>> ListarPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
                .ThenByDescending(m => m.CriadoEm)
            .ToListAsync();
    }

    // Usado pelo gerador de provas (item 14 do pedido: "por padrão mostra as
    // matrizes ATIVAS") — Historica/Rascunho só aparecem se o professor for
    // explicitamente à tela de gestão da matriz.
    public async Task<List<MatrizReferencia>> ListarAtivasPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .Where(m => m.Status == StatusMatrizReferencia.Ativa)
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
            .ToListAsync();
    }

    // Matrizes que o professor pode escolher pra CLASSIFICAR uma questão
    // (QuestaoForm.razor) — Ativa (a edição vigente) e Historica (pra ainda
    // classificar/reclassificar contra uma edição antiga, ver item 21:
    // questões já ligadas a ela continuam válidas). Rascunho fica de fora —
    // uma matriz ainda em montagem não deve receber vínculos de questão.
    public async Task<List<MatrizReferencia>> ListarVinculaveisPorCursoAsync(int cursoId)
    {
        var query = await QueryMatrizesDoCursoAsync(cursoId);
        return await query
            .Where(m => m.Status != StatusMatrizReferencia.Rascunho)
            .OrderBy(m => m.Tipo)
                .ThenByDescending(m => m.Ano)
            .ToListAsync();
    }

    // Todos os itens ATIVOS das matrizes vinculáveis do curso, de uma vez só
    // — usado por QuestaoForm.razor pra montar a lista de checkboxes (item 8
    // do pedido): como o total por curso costuma ser pequeno (dezenas, não
    // milhares — um ENADE inteiro tem ~30 itens), uma consulta só é mais
    // simples que recarregar a cada troca do seletor de Matriz; o filtro por
    // Matriz/Tipo/busca (item 31, "não renderizar centenas de checkboxes
    // indiscriminadamente") acontece em memória sobre essa lista já pequena.
    // Mesmo union por Área de Curso das demais listagens acima.
    public async Task<List<ItemMatrizReferencia>> ListarItensVinculaveisPorCursoAsync(int cursoId)
    {
        var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId);
        return await db.ItensMatrizReferencia
            .Include(i => i.MatrizReferencia)
            .Where(i => (i.MatrizReferencia!.CursoId == cursoId
                    || (areaCursoId != null && i.MatrizReferencia.AreaCursoId == areaCursoId))
                && i.MatrizReferencia.Status != StatusMatrizReferencia.Rascunho
                && i.Ativo)
            .OrderBy(i => i.MatrizReferencia!.Tipo)
                .ThenByDescending(i => i.MatrizReferencia!.Ano)
                .ThenBy(i => i.Tipo)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();
    }

    public Task<MatrizReferencia?> ObterAsync(int id) =>
        db.MatrizesReferencia.Include(m => m.Curso).Include(m => m.AreaCurso).FirstOrDefaultAsync(m => m.Id == id);

    // Sobrecarga autorizada (item 8/9): usada pelas telas de gestão
    // (MatrizForm/MatrizItensPage/MatrizItensImportar) no lugar da versão
    // acima. Devolve null tanto pra id inexistente quanto pra matriz sem
    // acesso de leitura (curso de OUTRA instituição) — as telas já tratam
    // "matriz is null" como "não encontrada" (nenhuma mudança de UI
    // necessária, e nenhuma informação extra vaza pra quem tenta um id
    // alheio). Matrizes de escopo nacional (AreaCursoId) são de leitura
    // aberta — ver TemAcessoDeLeituraAsync.
    public async Task<MatrizReferencia?> ObterAsync(int id, int? minhaInstituicaoId, bool ehAdmin)
    {
        var matriz = await ObterAsync(id);
        if (matriz is null)
        {
            return null;
        }
        return await TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin) ? matriz : null;
    }

    // Traz os itens junto — usado por MatrizItensPage.razor (gestão de
    // itens) e pelo dashboard de cobertura.
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
        return await TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin) ? matriz : null;
    }

    public async Task<MatrizReferencia> CriarAsync(MatrizReferenciaInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        ValidarMatriz(modelo);

        var nacional = modelo.Tipo.EhEscopoNacional();
        if (nacional)
        {
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await GarantirAcessoAoCursoAsync(modelo.CursoId, minhaInstituicaoId, ehAdmin);
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
        return matriz;
    }

    public async Task AtualizarAsync(int id, MatrizReferenciaInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        ValidarMatriz(modelo);

        var matriz = await db.MatrizesReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        // Autoriza pelo ESCOPO da matriz JÁ EXISTENTE (não pelo que veio do
        // formulário) — CursoId/AreaCursoId não são reatribuíveis aqui (ver
        // comentário abaixo), então é o valor de "matriz" que manda.
        await GarantirAcessoEscritaMatrizAsync(matriz, minhaInstituicaoId, ehAdmin);

        // Trocar de escopo (Curso <-> Área de Curso) na edição não é
        // permitido — mesmo raciocínio que já valia só pra CursoId antes
        // desta rodada: mudar o "dono" da matriz deixaria vínculos
        // (QuestaoItemMatriz) de questões do escopo antigo inconsistentes, e
        // uma matriz nacional virando institucional (ou vice-versa) no meio
        // do caminho não faz sentido acadêmico. Pra mover pra outro escopo,
        // crie uma matriz nova.
        var novoEscopoNacional = modelo.Tipo.EhEscopoNacional();
        var escopoAtualNacional = matriz.AreaCursoId is not null;
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

        // CursoId/AreaCursoId não são reatribuídos aqui de propósito — mesmo
        // raciocínio que já valia pra CursoId antes desta rodada: mudar a
        // matriz de curso/área deixaria vínculos (QuestaoItemMatriz) de
        // questões do escopo antigo inconsistentes. Pra mover, crie uma
        // matriz nova no curso/área certo.

        // Regra da matriz ENADE ativa única (item 11 do pedido, estendida
        // pro escopo nacional): só entra em jogo quando esta edição está
        // VIRANDO Ativa e é do Tipo ENADE — nesse caso, qualquer OUTRA
        // matriz ENADE do MESMO escopo (mesma AreaCurso, se nacional; mesmo
        // Curso, se institucional) que hoje esteja Ativa vira
        // automaticamente Histórica, na MESMA transação (item 20-like:
        // "nunca duas ENADE ativas ao mesmo tempo, nem que a segunda metade
        // da operação falhe"). PPC/Institucional/Outro NÃO entram nessa
        // regra (item 12: sem motivo dado pelo pedido pra restringi-los
        // também — decisão documentada no relatório final).
        if (matriz.Tipo == TipoMatrizReferencia.ENADE && matriz.Status == StatusMatrizReferencia.Ativa)
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
            return;
        }

        // Não é Tipo ENADE virando Ativa: nada mais a coordenar, é só a
        // matriz atual mudando de si mesma.
        await db.SaveChangesAsync();
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

    // Excluir de verdade só funciona pra matriz sem itens/vínculos (Restrict
    // no banco) — o caminho normal pra "aposentar" uma edição antiga é mudar
    // o Status pra Historica (ver item 21/22: nunca apagar, só marcar como
    // histórica), não excluir.
    public async Task ExcluirAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        await GarantirAcessoEscritaMatrizAsync(matriz, minhaInstituicaoId, ehAdmin);
        try
        {
            db.MatrizesReferencia.Remove(matriz);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{matriz.Nome}\": ela ainda tem itens cadastrados. Considere mudar o status para Histórica em vez de excluir.");
        }
    }

    // --- Item de Matriz ---

    public Task<List<ItemMatrizReferencia>> ListarItensAsync(int matrizId) =>
        db.ItensMatrizReferencia
            .Where(i => i.MatrizReferenciaId == matrizId)
            .OrderBy(i => i.Tipo)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();

    public Task<ItemMatrizReferencia?> ObterItemAsync(int id) =>
        db.ItensMatrizReferencia.Include(i => i.MatrizReferencia).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<ItemMatrizReferencia> CriarItemAsync(int matrizId, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await db.MatrizesReferencia.AnyAsync(m => m.Id == matrizId))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        await GarantirAcessoAMatrizAsync(matrizId, minhaInstituicaoId, ehAdmin);
        await ValidarItemAsync(matrizId, modelo, editandoId: null);

        var item = new ItemMatrizReferencia
        {
            MatrizReferenciaId = matrizId,
            Codigo = modelo.Codigo.Trim(),
            Titulo = modelo.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao,
            Tipo = modelo.Tipo,
            Ordem = modelo.Ordem,
            Ativo = modelo.Ativo,
        };

        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task AtualizarItemAsync(int id, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        var item = await db.ItensMatrizReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        await GarantirAcessoAoItemAsync(id, minhaInstituicaoId, ehAdmin);
        await ValidarItemAsync(item.MatrizReferenciaId, modelo, editandoId: id);

        item.Codigo = modelo.Codigo.Trim();
        item.Titulo = modelo.Titulo.Trim();
        item.Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao;
        item.Tipo = modelo.Tipo;
        item.Ordem = modelo.Ordem;
        item.Ativo = modelo.Ativo;

        await db.SaveChangesAsync();
    }

    // Código único dentro da matriz — checado aqui pra dar uma mensagem
    // amigável (o índice único no banco, ver ApplicationDbContext, é quem
    // garante isso de verdade se dois cadastros colidirem ao mesmo tempo).
    private async Task ValidarItemAsync(int matrizId, ItemMatrizInput modelo, int? editandoId)
    {
        if (string.IsNullOrWhiteSpace(modelo.Codigo))
        {
            throw new OperacaoInvalidaException("Informe o código do item (ex.: \"C01\").");
        }

        if (string.IsNullOrWhiteSpace(modelo.Titulo))
        {
            throw new OperacaoInvalidaException("Informe o título do item.");
        }

        var codigo = modelo.Codigo.Trim();
        var jaExiste = await db.ItensMatrizReferencia
            .AnyAsync(i => i.MatrizReferenciaId == matrizId && i.Id != (editandoId ?? 0) && EF.Functions.ILike(i.Codigo, codigo));

        if (jaExiste)
        {
            throw new OperacaoInvalidaException($"Já existe um item com o código \"{codigo}\" nessa matriz.");
        }
    }

    // Desativar em vez de excluir preserva o histórico de questões já
    // vinculadas (ver ItemMatrizReferencia.Ativo) — é o caminho padrão pra
    // "remover" um item já em uso.
    public async Task AlternarAtivoItemAsync(int id, int? minhaInstituicaoId, bool ehAdmin)
    {
        var item = await db.ItensMatrizReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        await GarantirAcessoAoItemAsync(id, minhaInstituicaoId, ehAdmin);

        item.Ativo = !item.Ativo;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirItemAsync(ItemMatrizReferencia item, int? minhaInstituicaoId, bool ehAdmin)
    {
        await GarantirAcessoAoItemAsync(item.Id, minhaInstituicaoId, ehAdmin);
        try
        {
            db.ItensMatrizReferencia.Remove(item);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{item.Codigo} — {item.Titulo}\": ele ainda tem questões vinculadas. Considere desativá-lo em vez de excluir.");
        }
    }

    // --- Importação CSV/XLSX/JSON (ver Importacao/ItemMatrizImportParser.cs) ---

    // Aplica no banco o resultado já revisado da pré-visualização — o parser
    // só interpreta o arquivo, sem tocar no banco. Faz UPSERT por Código
    // dentro da matriz: reimportar um arquivo corrigido atualiza quem já
    // existe em vez de duplicar (mesmo espírito do antigo
    // CursoService.ImportarDiretrizesAsync, mas bem mais simples aqui — sem
    // resolução de pai em duas passadas, porque itens são flat).
    public async Task<int> ImportarItensAsync(int matrizId, List<ItemMatrizImportado> linhas, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await db.MatrizesReferencia.AnyAsync(m => m.Id == matrizId))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        await GarantirAcessoAMatrizAsync(matrizId, minhaInstituicaoId, ehAdmin);

        // Transação (item 20 do pedido): se falhar no meio (ex.: item 30 de
        // 36), a matriz não pode ficar com metade dos itens importados e a
        // outra metade não — ou importa tudo, ou nada muda. Não há
        // ExecutionStrategy configurada em Program.cs (UseNpgsql sem
        // EnableRetryOnFailure), então uma transação simples é suficiente,
        // sem precisar de CreateExecutionStrategy().ExecuteAsync.
        using var transacao = await db.Database.BeginTransactionAsync();
        var importados = await ImportarItensSemTransacaoAsync(matrizId, linhas);
        await transacao.CommitAsync();
        return importados;
    }

    // Núcleo do upsert-por-Código, SEM abrir transação própria — usado tanto
    // por ImportarItensAsync acima (que abre a SUA transação) quanto por
    // ImportarMatrizCompletaAsync abaixo (item 17-20: matriz nova + itens
    // precisam entrar na MESMA transação, e o EF Core/Npgsql não suporta
    // abrir uma segunda transação dentro de uma já aberta na mesma conexão).
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

    // --- Validação de vínculo Questão <-> Item (usado por QuestaoService) ---

    // Só devolve os itens que realmente pertencem ao curso informado — nunca
    // confia cegamente nos ids vindos do formulário (item 26 do pedido:
    // "Questao -> Curso" e "ItemMatriz -> Matriz -> Curso" precisam
    // coincidir, validado sempre no servidor). Uma questão pode escolher
    // itens de MAIS DE UMA matriz do mesmo curso ao mesmo tempo (item 10) —
    // por isso o filtro é só por CursoId da matriz-mãe, não por uma
    // MatrizReferenciaId específica.
    // Mesmo union por Área de Curso das listagens acima (item 26 continua
    // valendo: nunca confia cegamente nos ids vindos do formulário — só que
    // agora "pertencer ao curso" inclui itens de uma matriz nacional
    // ENADE/DCN vinculada à Área de Curso desse curso).
    public async Task<List<ItemMatrizReferencia>> ValidarItensDoCursoAsync(int? cursoId, IEnumerable<int> itemIds)
    {
        var ids = itemIds.ToList();
        if (cursoId is null || ids.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        var areaCursoId = await ObterAreaCursoIdDoCursoAsync(cursoId.Value);

        return await db.ItensMatrizReferencia
            .Where(i => (i.MatrizReferencia!.CursoId == cursoId.Value
                    || (areaCursoId != null && i.MatrizReferencia.AreaCursoId == areaCursoId))
                && ids.Contains(i.Id))
            .ToListAsync();
    }

    // --- Diagnóstico pré-geração (itens 17/18 do pedido) ---

    // Quantas questões ATIVAS e VISÍVEIS pro professor já estão vinculadas a
    // cada item — pra tela mostrar "C01 — 32 questões disponíveis" ANTES de
    // gerar a prova, e destacar (⚠) quando a quantidade pedida no blueprint é
    // maior do que a disponível. Tudo agregado no banco (GroupBy vira SQL).
    public async Task<Dictionary<int, int>> ContarQuestoesDisponiveisPorItemAsync(
        IEnumerable<int> itemIds, string? meuId, int? minhaInstituicaoId)
    {
        var ids = itemIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        // Reaproveita QuestaoVisibilidade.VisivelPara (item 15 do pedido) em
        // vez de reescrever a expressão Compartilhada/CriadoPorId/
        // Institucional aqui dentro — antes desta correção esse mesmo
        // filtro estava duplicado à mão, podendo divergir da regra "oficial"
        // se uma das duas cópias fosse alterada sem a outra.
        return await db.QuestoesItensMatriz
            .Where(qi => ids.Contains(qi.ItemMatrizReferenciaId))
            .Where(qi => db.Questoes.AsQueryable().VisivelPara(meuId, minhaInstituicaoId)
                .Any(q => q.Id == qi.QuestaoId && q.Ativa))
            .GroupBy(qi => qi.ItemMatrizReferenciaId)
            .Select(g => new { ItemId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantidade);
    }

    // Quantas questões DISTINTAS (não a soma ingênua por item) atendem a
    // pelo menos UM dos itens do conjunto selecionado — item 4/5 do pedido:
    // C08 com 10 disponíveis e CT20 com 10 disponíveis podem ser as MESMAS
    // 10 questões, então "10 + 10 = 20" seria enganoso. Contado direto no
    // banco (Distinct por QuestaoId), sem carregar questões pra memória.
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

    // --- Dashboard: Cobertura Curricular (item 19, escopado a UMA matriz) ---

    public async Task<CoberturaCurricularResultado> ObterCoberturaCurricularAsync(
        int matrizId, string? meuId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var matriz = await db.MatrizesReferencia
            .Include(m => m.Curso)
            .Include(m => m.AreaCurso)
            .FirstOrDefaultAsync(m => m.Id == matrizId)
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        // Item 8/9/14 do pedido: o dashboard institucional só pode ser visto
        // por quem tem relação com o curso (mesma instituição, ou Admin) —
        // sem isso, "totalQuestoes" abaixo contava TODAS as questões ativas
        // do curso pra QUALQUER usuário autenticado, de qualquer instituição.
        // Matriz de escopo nacional (AreaCursoId) é de leitura aberta — ver
        // TemAcessoDeLeituraAsync.
        if (!await TemAcessoDeLeituraAsync(matriz, minhaInstituicaoId, ehAdmin))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        var itens = await ListarItensAsync(matrizId);

        // Matriz curso-escopada: cobertura de UM curso, como sempre foi.
        // Matriz nacional (AreaCursoId): não existe "o curso" — a cobertura
        // conta questões de QUALQUER curso que aponte pra essa mesma Área de
        // Curso (ver Curso.AreaCursoId), já que é exatamente esse o conjunto
        // de questões que essa matriz nacional pode classificar.
        var totalQuestoes = matriz.CursoId is int cursoId
            ? await db.Questoes.CountAsync(q => q.CursoId == cursoId && q.Ativa)
            : await db.Questoes.CountAsync(q => q.Curso!.AreaCursoId == matriz.AreaCursoId && q.Ativa);

        var totalQuestoesMinhas = matriz.CursoId is int cursoId2
            ? await db.Questoes.AsQueryable()
                .VisivelPara(meuId, minhaInstituicaoId)
                .CountAsync(q => q.CursoId == cursoId2 && q.Ativa)
            : await db.Questoes.AsQueryable()
                .VisivelPara(meuId, minhaInstituicaoId)
                .CountAsync(q => q.Curso!.AreaCursoId == matriz.AreaCursoId && q.Ativa);

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

    // --- Importação de MATRIZ COMPLETA via JSON (item 17-20 da 2ª rodada de revisão) ---
    //
    // Fluxo em duas etapas, igual ImportarItensAsync já fazia pra itens
    // avulsos: 1) PrepararImportacaoMatrizCompletaAsync monta uma PRÉVIA
    // (curso encontrado ou não, tipo resolvido, contagens por grupo, se já
    // existe uma matriz parecida) SEM tocar no banco; 2)
    // ImportarMatrizCompletaAsync só roda depois que o professor confirma
    // explicitamente curso/tipo na tela (item 18: "NUNCA criar curso
    // automaticamente sem confirmação" — aqui o professor sempre escolhe o
    // Curso de um <select>, nunca é criado a partir do texto do JSON).

    // Acha um Curso pelo NOME exato (case-insensitive) só dentro do que o
    // usuário pode acessar (mesma instituição, ou Admin) — item 8/9: mesmo
    // um JSON malicioso com o nome de um curso de outra instituição não
    // "encontra" nada aqui, e a tela vai pedir pra escolher manualmente
    // (que também vem de uma lista já escopada — ver CursoService.
    // ListarPorInstituicaoAsync).
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

    // Mesmo espírito de LocalizarCursoAsync, mas pra Área de Curso (usado
    // quando o Tipo resolvido é nacional — ENADE/DCN). Sem filtro por
    // instituição de propósito: AreaCurso é um catálogo nacional, não
    // pertence a nenhuma instituição (ver AreaCursoService).
    private async Task<AreaCurso?> LocalizarAreaCursoAsync(string? nomeArea)
    {
        if (string.IsNullOrWhiteSpace(nomeArea))
        {
            return null;
        }

        return await db.AreasCurso.FirstOrDefaultAsync(a => EF.Functions.ILike(a.Nome, nomeArea.Trim()));
    }

    // Mesmo espírito de ItemMatrizImportParser.ResolverTipo (aceita nome do
    // enum OU rótulo em português), mas pra TipoMatrizReferencia — devolve
    // null (em vez de cair num "Outro" como itens fazem) porque aqui o
    // professor SEMPRE confirma o Tipo na prévia antes de importar; não
    // tem "ajuste depois" pra Tipo de Matriz do jeito que tem pra Tipo de
    // Item.
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
        // Tenta localizar o texto de "curso" do JSON tanto como Curso quanto
        // como Área de Curso — barato (duas consultas por nome) e evita
        // precisar de um campo novo no JSON só pra dizer "isso aqui é uma
        // área, não um curso"; qual dos dois é o relevante só se sabe depois
        // que o Tipo é resolvido/confirmado (ver abaixo e a tela, que mostra
        // o seletor certo assim que o professor escolhe/confirma o Tipo).
        var curso = await LocalizarCursoAsync(dados.Curso, minhaInstituicaoId, ehAdmin);
        var area = await LocalizarAreaCursoAsync(dados.Curso);
        var tipo = ResolverTipoMatriz(dados.Tipo);

        // "Matriz parecida" (item 19): mesmo escopo (Área pra ENADE/DCN,
        // Curso pros demais) + tipo + ano — só dá pra checar quando o Tipo já
        // foi resolvido E o escopo correspondente foi encontrado.
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

    // Confirma a importação: cria a MatrizReferencia (sempre Rascunho — o
    // professor ativa deliberadamente depois, mesmo caminho de uma matriz
    // criada manualmente) e importa os itens, TUDO na mesma transação (item
    // 20: nunca deixar matriz criada com só metade dos itens). Recebe
    // cursoId E areaCursoId (um dos dois, conforme o Tipo é institucional ou
    // nacional — ver MatrizReferencia) porque o professor pode ter trocado o
    // curso/área sugerido pelo parser por outro do seletor.
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

        // Item 8/9/18: nunca confia só na UI — revalida acesso ao escopo
        // ESCOLHIDO antes de criar qualquer coisa.
        if (nacional)
        {
            if (areaCursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione a Área de Curso correspondente.");
            }
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            if (cursoId is null or 0)
            {
                throw new OperacaoInvalidaException("Selecione o curso correspondente.");
            }
            await GarantirAcessoAoCursoAsync(cursoId.Value, minhaInstituicaoId, ehAdmin);
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

// Prévia da importação de matriz completa (item 17/18/19 do pedido) — o que
// a tela de importação mostra ANTES de qualquer escrita no banco.
public sealed class PreviaImportacaoMatrizCompleta
{
    public required string? CursoTexto { get; init; }
    public required Curso? CursoEncontrado { get; init; }
    // Preenchida quando o texto de "curso" do JSON bate com o NOME de uma
    // Área de Curso cadastrada — relevante só quando Tipo é ENADE/DCN (ver
    // TipoMatrizReferenciaExtensions.EhEscopoNacional). CursoEncontrado e
    // AreaCursoEncontrada podem vir os dois preenchidos ao mesmo tempo (o
    // texto pode coincidir com ambos) — quem decide qual dos dois é o
    // relevante é o Tipo, não esta prévia.
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
