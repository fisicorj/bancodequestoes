using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Curso e Turma moram no mesmo Service porque Turma sempre pertence a um
// Curso; ProvaForm/ProvaService reusam as listagens daqui.
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

    // ATENÇÃO: lista cursos de QUALQUER instituição, sem filtro por usuário. Pra Matriz, use ListarPorInstituicaoAsync abaixo.
    public Task<List<Curso>> ListarTodosAsync() =>
        db.Cursos.Include(c => c.Instituicao).OrderBy(c => c.Nome).ToListAsync();

    // Cursos da mesma instituição do usuário; sem instituição no perfil, devolve lista vazia.
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

        // Mantém CursoDisciplina em sincronia com a Turma (ver GarantirCursoDisciplinaAsync abaixo).
        await GarantirCursoDisciplinaAsync(modelo.CursoId, modelo.DisciplinaId);

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

        await GarantirCursoDisciplinaAsync(modelo.CursoId, modelo.DisciplinaId);

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

    // Bimestre só é obrigatório quando a Instituição do Curso usa SemestralComBimestres.
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

    // Matriz de Referência/Item/Cobertura Curricular moram em MatrizReferenciaService à parte.

    // CursoDisciplina fica aqui por ser pequeno; preenchido automaticamente
    // por GarantirCursoDisciplinaAsync a cada Turma criada/editada.

    public Task<List<CursoDisciplina>> ListarDisciplinasDoCursoAsync(int cursoId) =>
        db.CursosDisciplinas
            .Include(cd => cd.Disciplina)
            .Where(cd => cd.CursoId == cursoId)
            .OrderBy(cd => cd.Disciplina!.Nome)
            .ToListAsync();

    public Task<CursoDisciplina?> ObterCursoDisciplinaAsync(int id) =>
        db.CursosDisciplinas.Include(cd => cd.Curso).Include(cd => cd.Disciplina).FirstOrDefaultAsync(cd => cd.Id == id);

    public async Task<CursoDisciplina> CriarCursoDisciplinaAsync(CursoDisciplinaInput modelo)
    {
        await ValidarCursoDisciplinaAsync(modelo, editandoId: null);

        var vinculo = new CursoDisciplina
        {
            CursoId = modelo.CursoId,
            DisciplinaId = modelo.DisciplinaId,
            Semestre = modelo.Semestre,
            CargaHoraria = modelo.CargaHoraria,
            Ativa = modelo.Ativa,
        };

        db.CursosDisciplinas.Add(vinculo);
        await db.SaveChangesAsync();
        return vinculo;
    }

    public async Task AtualizarCursoDisciplinaAsync(int id, CursoDisciplinaInput modelo)
    {
        await ValidarCursoDisciplinaAsync(modelo, editandoId: id);

        var vinculo = await db.CursosDisciplinas.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Vínculo Curso/Disciplina não encontrado.");

        vinculo.CursoId = modelo.CursoId;
        vinculo.DisciplinaId = modelo.DisciplinaId;
        vinculo.Semestre = modelo.Semestre;
        vinculo.CargaHoraria = modelo.CargaHoraria;
        vinculo.Ativa = modelo.Ativa;
        await db.SaveChangesAsync();
    }

    // Índice único (CursoId, DisciplinaId) garante isso numa corrida real —
    // checagem aqui é só pra mensagem amigável.
    private async Task ValidarCursoDisciplinaAsync(CursoDisciplinaInput modelo, int? editandoId)
    {
        if (modelo.CursoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione um curso.");
        }

        if (modelo.DisciplinaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma disciplina.");
        }

        var jaExiste = await db.CursosDisciplinas.AnyAsync(cd =>
            cd.Id != (editandoId ?? 0) && cd.CursoId == modelo.CursoId && cd.DisciplinaId == modelo.DisciplinaId);

        if (jaExiste)
        {
            throw new OperacaoInvalidaException("Essa disciplina já está na grade deste curso.");
        }
    }

    public async Task ExcluirCursoDisciplinaAsync(CursoDisciplina vinculo)
    {
        try
        {
            db.CursosDisciplinas.Remove(vinculo);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                "Não foi possível excluir esse vínculo: verifique se ainda existem Turmas dessa disciplina nesse curso.");
        }
    }

    // Upsert lógico: cria CursoDisciplina quando a Turma usa um par novo, sem nunca bloquear a Turma.
    private async Task GarantirCursoDisciplinaAsync(int cursoId, int disciplinaId)
    {
        var existe = await db.CursosDisciplinas.AnyAsync(cd => cd.CursoId == cursoId && cd.DisciplinaId == disciplinaId);
        if (!existe)
        {
            db.CursosDisciplinas.Add(new CursoDisciplina { CursoId = cursoId, DisciplinaId = disciplinaId, Ativa = true });
        }
    }
}
