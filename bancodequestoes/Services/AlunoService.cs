using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Aluno e TurmaAluno (matrícula) moram juntos, mesmo padrão de CursoService (Curso+Turma)
// — matrícula só existe em função de um Aluno numa Turma.
public class AlunoService(ApplicationDbContext db)
{
    // --- Aluno ---

    public async Task<PaginaResultado<Aluno>> ListarAsync(string? filtroTexto, int filtroInstituicaoId, int filtroTurmaId, int pagina, int tamanhoPagina)
    {
        var query = db.Alunos.Include(a => a.Instituicao).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(a =>
                EF.Functions.ILike(a.Nome, $"%{filtroTexto}%") ||
                (a.Email != null && EF.Functions.ILike(a.Email, $"%{filtroTexto}%")) ||
                (a.Matricula != null && EF.Functions.ILike(a.Matricula, $"%{filtroTexto}%")));
        }

        if (filtroInstituicaoId != 0)
        {
            query = query.Where(a => a.InstituicaoId == filtroInstituicaoId);
        }

        if (filtroTurmaId != 0)
        {
            query = query.Where(a => a.TurmaAlunos.Any(ta => ta.TurmaId == filtroTurmaId && ta.Ativa));
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderBy(a => a.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Aluno> { Itens = itens, Total = total };
    }

    // Usado pelo seletor de "matricular aluno" — só ativos, sem paginação.
    public Task<List<Aluno>> ListarTodosAsync(int? instituicaoId = null) =>
        db.Alunos
            .Where(a => a.Ativo && (instituicaoId == null || a.InstituicaoId == instituicaoId))
            .OrderBy(a => a.Nome)
            .ToListAsync();

    public Task<Aluno?> ObterAsync(int id) =>
        db.Alunos.Include(a => a.Instituicao).FirstOrDefaultAsync(a => a.Id == id);

    public async Task<Aluno> CriarAsync(AlunoInput modelo)
    {
        var aluno = new Aluno
        {
            Nome = modelo.Nome,
            Email = Normalizar(modelo.Email),
            Matricula = Normalizar(modelo.Matricula),
            InstituicaoId = modelo.InstituicaoId,
        };
        db.Alunos.Add(aluno);
        await db.SaveChangesAsync();
        return aluno;
    }

    public async Task AtualizarAsync(int id, AlunoInput modelo)
    {
        var aluno = await db.Alunos.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Aluno não encontrado.");

        aluno.Nome = modelo.Nome;
        aluno.Email = Normalizar(modelo.Email);
        aluno.Matricula = Normalizar(modelo.Matricula);
        aluno.InstituicaoId = modelo.InstituicaoId;
        await db.SaveChangesAsync();
    }

    // Desativar é o caminho padrão pra "remover" sem perder histórico de matrículas/tentativas;
    // exclusão física é bloqueada pelo banco (FK Restrict) se o aluno estiver em uso.
    public async Task AlternarAtivoAsync(Aluno aluno)
    {
        aluno.Ativo = !aluno.Ativo;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Aluno aluno)
    {
        try
        {
            db.Alunos.Remove(aluno);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{aluno.Nome}\": verifique se ele ainda tem matrículas ou tentativas de prova vinculadas.");
        }
    }

    private static string? Normalizar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    // --- Matrícula (TurmaAluno) ---

    public Task<List<TurmaAluno>> ListarMatriculasDaTurmaAsync(int turmaId) =>
        db.TurmasAlunos
            .Include(ta => ta.Aluno)
            .Where(ta => ta.TurmaId == turmaId)
            .OrderBy(ta => ta.Aluno!.Nome)
            .ToListAsync();

    public Task<List<TurmaAluno>> ListarTurmasDoAlunoAsync(int alunoId) =>
        db.TurmasAlunos
            .Include(ta => ta.Turma)
                .ThenInclude(t => t!.Curso)
            .Where(ta => ta.AlunoId == alunoId)
            .OrderByDescending(ta => ta.Turma!.Ano)
                .ThenByDescending(ta => ta.Turma!.Semestre)
            .ToListAsync();

    // Upsert: matrícula já existente (mesmo desativada) é reaproveitada/reativada em vez de
    // duplicar — o índice único (TurmaId, AlunoId) não permite duas linhas pro mesmo par.
    public async Task<TurmaAluno> MatricularAsync(int turmaId, int alunoId)
    {
        var existente = await db.TurmasAlunos.FirstOrDefaultAsync(ta => ta.TurmaId == turmaId && ta.AlunoId == alunoId);
        if (existente is not null)
        {
            if (existente.Ativa)
            {
                throw new OperacaoInvalidaException("Esse aluno já está matriculado nessa turma.");
            }

            existente.Ativa = true;
            await db.SaveChangesAsync();
            return existente;
        }

        var matricula = new TurmaAluno { TurmaId = turmaId, AlunoId = alunoId };
        db.TurmasAlunos.Add(matricula);
        await db.SaveChangesAsync();
        return matricula;
    }

    public async Task AlternarAtivoMatriculaAsync(TurmaAluno matricula)
    {
        matricula.Ativa = !matricula.Ativa;
        await db.SaveChangesAsync();
    }

    // Nada referencia TurmaAluno por FK, então a exclusão física não precisa de try/catch.
    public async Task ExcluirMatriculaAsync(TurmaAluno matricula)
    {
        db.TurmasAlunos.Remove(matricula);
        await db.SaveChangesAsync();
    }

    // --- Importação em lote (CSV) ---

    // Recebe as linhas já interpretadas (AlunoImportParser.ParseCsv) e SELECIONADAS na
    // pré-visualização; casa por Matricula ou Email (dentro da mesma Instituição) pra não
    // duplicar aluno já cadastrado — o resto vira Aluno novo. TurmaId opcional já matricula.
    public async Task<ResumoImportacaoAlunos> ImportarAsync(List<AlunoImportado> selecionados, int? instituicaoId, int? turmaId)
    {
        var resumo = new ResumoImportacaoAlunos();

        foreach (var linha in selecionados)
        {
            var existente = await LocalizarExistenteAsync(linha, instituicaoId);

            Aluno aluno;
            if (existente is not null)
            {
                aluno = existente;
                resumo.AlunosReaproveitados++;
            }
            else
            {
                aluno = new Aluno
                {
                    Nome = linha.Nome,
                    Email = Normalizar(linha.Email),
                    Matricula = Normalizar(linha.Matricula),
                    InstituicaoId = instituicaoId,
                };
                db.Alunos.Add(aluno);
                await db.SaveChangesAsync();
                resumo.AlunosCriados++;
            }

            if (turmaId is int id)
            {
                var matricula = await db.TurmasAlunos.FirstOrDefaultAsync(ta => ta.TurmaId == id && ta.AlunoId == aluno.Id);
                if (matricula is null)
                {
                    db.TurmasAlunos.Add(new TurmaAluno { TurmaId = id, AlunoId = aluno.Id });
                    resumo.Matriculas++;
                }
                else if (!matricula.Ativa)
                {
                    matricula.Ativa = true;
                    resumo.Matriculas++;
                }
            }
        }

        await db.SaveChangesAsync();
        return resumo;
    }

    private async Task<Aluno?> LocalizarExistenteAsync(AlunoImportado linha, int? instituicaoId)
    {
        if (!string.IsNullOrWhiteSpace(linha.Matricula))
        {
            var porMatricula = await db.Alunos.FirstOrDefaultAsync(a =>
                a.Matricula == linha.Matricula && (instituicaoId == null || a.InstituicaoId == instituicaoId));
            if (porMatricula is not null)
            {
                return porMatricula;
            }
        }

        if (!string.IsNullOrWhiteSpace(linha.Email))
        {
            return await db.Alunos.FirstOrDefaultAsync(a =>
                a.Email == linha.Email && (instituicaoId == null || a.InstituicaoId == instituicaoId));
        }

        return null;
    }
}

// Contadores devolvidos pra tela mostrar um resumo amigável depois de importar.
public sealed class ResumoImportacaoAlunos
{
    public int AlunosCriados { get; set; }
    public int AlunosReaproveitados { get; set; }
    public int Matriculas { get; set; }
}
