using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Curso e Turma moram no mesmo Service de propósito: Turma não faz sentido
// sem um Curso (é sempre "oferta de uma disciplina dentro de um curso"),
// mesmo raciocínio do Disciplina+Assunto no DisciplinaService.
//
// ProvaForm/ProvaService reusam ListarTodosAsync/ListarTodasTurmasAsync
// daqui em vez de duplicar essas consultas — mesmo padrão de ProvaForm
// injetar DisciplinaService direto pra lista de disciplinas.
public class CursoService(ApplicationDbContext db)
{
    // --- Curso ---

    public async Task<PaginaResultado<Curso>> ListarAsync(string? filtroTexto, int filtroInstituicaoId, int pagina, int tamanhoPagina)
    {
        var query = db.Cursos.Include(c => c.Instituicao).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(c => EF.Functions.ILike(c.Nome, $"%{filtroTexto}%"));
        }

        if (filtroInstituicaoId != 0)
        {
            query = query.Where(c => c.InstituicaoId == filtroInstituicaoId);
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Curso> { Itens = itens, Total = total };
    }

    // Usado pelos seletores (Turma, Prova) que precisam de "todos os cursos"
    // com a Instituição já carregada, sem paginação.
    //
    // ATENÇÃO (item 10 da 2ª rodada de revisão): isso lista cursos de
    // QUALQUER instituição, sem nenhum filtro por usuário — é um gap
    // pré-existente (Curso/Instituicao/Turma nunca tiveram escopo por
    // usuário no projeto) que permanece aqui, fora do escopo desta rodada
    // (que tratou só as telas de Matriz de Referência). Continua sendo o
    // método certo pra telas que já são globais por natureza (ex.: Admin) ou
    // pra Turma/Prova, que não foram tocadas nesta rodada. Pra Matriz de
    // Referência, use ListarPorInstituicaoAsync abaixo em vez deste.
    public Task<List<Curso>> ListarTodosAsync() =>
        db.Cursos.Include(c => c.Instituicao).OrderBy(c => c.Nome).ToListAsync();

    // Cursos da MESMA instituição do usuário — usado pelos seletores de
    // Curso nas telas de Matriz de Referência (MatrizesPage/MatrizForm/
    // CoberturaCurricularPage), pra um professor não conseguir nem LISTAR
    // cursos de outra instituição no dropdown (item 8/9/10 do pedido). Sem
    // instituição definida no perfil, devolve lista vazia — a tela orienta a
    // definir a instituição no perfil, mesmo padrão já usado em
    // QuestaoForm.razor pra Visibilidade Institucional.
    public Task<List<Curso>> ListarPorInstituicaoAsync(int? instituicaoId) =>
        instituicaoId is null
            ? Task.FromResult(new List<Curso>())
            : db.Cursos.Include(c => c.Instituicao).Where(c => c.InstituicaoId == instituicaoId).OrderBy(c => c.Nome).ToListAsync();

    public Task<Curso?> ObterAsync(int id) => db.Cursos.FindAsync(id).AsTask();

    public async Task<Curso> CriarAsync(CursoInput modelo)
    {
        if (modelo.InstituicaoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma instituição.");
        }

        var curso = new Curso { Nome = modelo.Nome, InstituicaoId = modelo.InstituicaoId, AreaCursoId = modelo.AreaCursoId };
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        return curso;
    }

    public async Task AtualizarAsync(int id, CursoInput modelo)
    {
        if (modelo.InstituicaoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma instituição.");
        }

        var curso = await db.Cursos.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Curso não encontrado.");

        curso.Nome = modelo.Nome;
        curso.InstituicaoId = modelo.InstituicaoId;
        curso.AreaCursoId = modelo.AreaCursoId;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Curso curso)
    {
        try
        {
            db.Cursos.Remove(curso);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{curso.Nome}\": verifique se ele ainda tem Turmas vinculadas.");
        }
    }

    // --- Turma ---

    public async Task<PaginaResultado<Turma>> ListarTurmasAsync(string? filtroTexto, int filtroCursoId, int filtroDisciplinaId, int pagina, int tamanhoPagina)
    {
        var query = db.Turmas
            .Include(t => t.Curso)
                .ThenInclude(c => c!.Instituicao)
            .Include(t => t.Disciplina)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{filtroTexto}%"));
        }

        if (filtroCursoId != 0)
        {
            query = query.Where(t => t.CursoId == filtroCursoId);
        }

        if (filtroDisciplinaId != 0)
        {
            query = query.Where(t => t.DisciplinaId == filtroDisciplinaId);
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(t => t.Ano)
                .ThenByDescending(t => t.Semestre)
                .ThenBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Turma> { Itens = itens, Total = total };
    }

    // Usado pelo seletor de Turma em ProvaForm — turmas mais recentes primeiro,
    // sem paginação.
    public Task<List<Turma>> ListarTodasTurmasAsync() =>
        db.Turmas
            .Include(t => t.Curso)
                .ThenInclude(c => c!.Instituicao)
            .Include(t => t.Disciplina)
            .OrderByDescending(t => t.Ano)
                .ThenByDescending(t => t.Semestre)
                .ThenBy(t => t.Nome)
            .ToListAsync();

    public Task<Turma?> ObterTurmaAsync(int id) => db.Turmas.FindAsync(id).AsTask();

    public async Task<Turma> CriarTurmaAsync(TurmaInput modelo, string? criadoPorId)
    {
        await ValidarTurmaAsync(modelo);

        var turma = new Turma
        {
            Nome = modelo.Nome,
            CursoId = modelo.CursoId,
            DisciplinaId = modelo.DisciplinaId,
            Ano = modelo.Ano,
            Semestre = modelo.Semestre,
            Bimestre = modelo.Bimestre,
            CriadoPorId = criadoPorId,
        };

        db.Turmas.Add(turma);
        await db.SaveChangesAsync();
        return turma;
    }

    public async Task AtualizarTurmaAsync(int id, TurmaInput modelo)
    {
        await ValidarTurmaAsync(modelo);

        var turma = await db.Turmas.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Turma não encontrada.");

        turma.Nome = modelo.Nome;
        turma.CursoId = modelo.CursoId;
        turma.DisciplinaId = modelo.DisciplinaId;
        turma.Ano = modelo.Ano;
        turma.Semestre = modelo.Semestre;
        turma.Bimestre = modelo.Bimestre;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirTurmaAsync(Turma turma)
    {
        try
        {
            db.Turmas.Remove(turma);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{turma.Rotulo}\": ela está vinculada a uma ou mais provas. Remova o vínculo antes de excluir.");
        }
    }

    // Bimestre só é obrigatório quando a Instituição do Curso escolhido usa
    // SemestralComBimestres — daí precisar ir ao banco (deixou de ser
    // síncrono) pra descobrir isso a partir do CursoId antes de validar.
    private async Task ValidarTurmaAsync(TurmaInput modelo)
    {
        if (modelo.CursoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione um curso.");
        }

        if (modelo.DisciplinaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma disciplina.");
        }

        var sistemaPeriodos = await db.Cursos
            .Where(c => c.Id == modelo.CursoId)
            .Select(c => (SistemaPeriodos?)c.Instituicao!.SistemaPeriodos)
            .FirstOrDefaultAsync();

        if (sistemaPeriodos == SistemaPeriodos.SemestralComBimestres && modelo.Bimestre is not (1 or 2))
        {
            throw new OperacaoInvalidaException("Essa instituição usa bimestres — selecione o 1º ou 2º bimestre desse semestre.");
        }
    }

    // --- Alinhamento Curricular / ENADE ---
    //
    // A gestão de Matriz de Referência / Item de Matriz / Cobertura
    // Curricular NÃO mora aqui (diferente da antiga DiretrizCurricular, que
    // vivia neste Service) — vive em MatrizReferenciaService à parte. Motivo:
    // ali existe um conceito novo (versionamento — várias matrizes por
    // curso, com Status Rascunho/Ativa/Historica) e uma superfície bem maior
    // (matriz + itens + validação de vínculo questão/curso + cobertura), que
    // deixaria este Service, já grande com Curso+Turma, difícil de navegar.
    // Este Service continua sendo quem CursoForm/CursoList injetam pra
    // Curso/Turma; a área "Matrizes de Referência" da tela de Curso injeta
    // MatrizReferenciaService à parte.

}
