using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa AlunoService: CRUD, upsert de matrícula (TurmaAluno) e importação CSV com dedupe
// por Matricula/Email. Não cobre o catch de DbUpdateException em ExcluirAsync — mesma
// limitação do InMemory já documentada em TestDbFactory.
public class AlunoServiceTests
{
    private static async Task<Turma> SeedTurmaAsync(BancoQuestoes.Data.ApplicationDbContext db)
    {
        var curso = TestSeed.Curso("Engenharia");
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var turma = TestSeed.Turma("EC1A", curso.Id, disciplina.Id);
        db.Turmas.Add(turma);
        await db.SaveChangesAsync();
        return turma;
    }

    [Fact]
    public async Task CriarAsync_Basico_Salva()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AlunoService(db);

        var aluno = await servico.CriarAsync(new AlunoInput { Nome = "Maria", Email = " maria@exemplo.com ", Matricula = " 2026001 " });

        Assert.Equal("Maria", aluno.Nome);
        Assert.Equal("maria@exemplo.com", aluno.Email);
        Assert.Equal("2026001", aluno.Matricula);
        Assert.True(aluno.Ativo);
    }

    [Fact]
    public async Task AtualizarAsync_Inexistente_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AlunoService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.AtualizarAsync(999, new AlunoInput { Nome = "X" }));
    }

    [Fact]
    public async Task AlternarAtivoAsync_Desativa_ENaoAparece_EmListarTodosAsync()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AlunoService(db);
        var aluno = await servico.CriarAsync(new AlunoInput { Nome = "Maria" });

        await servico.AlternarAtivoAsync(aluno);

        Assert.False(aluno.Ativo);
        Assert.DoesNotContain(await servico.ListarTodosAsync(), a => a.Id == aluno.Id);
    }

    [Fact]
    public async Task MatricularAsync_PrimeiraVez_Cria()
    {
        using var db = TestDbFactory.Criar();
        var turma = await SeedTurmaAsync(db);
        var servico = new AlunoService(db);
        var aluno = await servico.CriarAsync(new AlunoInput { Nome = "Maria" });

        var matricula = await servico.MatricularAsync(turma.Id, aluno.Id);

        Assert.True(matricula.Ativa);
    }

    [Fact]
    public async Task MatricularAsync_JaMatriculadoAtivo_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var turma = await SeedTurmaAsync(db);
        var servico = new AlunoService(db);
        var aluno = await servico.CriarAsync(new AlunoInput { Nome = "Maria" });
        await servico.MatricularAsync(turma.Id, aluno.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => servico.MatricularAsync(turma.Id, aluno.Id));
    }

    [Fact]
    public async Task MatricularAsync_Desmatriculado_Reativa()
    {
        using var db = TestDbFactory.Criar();
        var turma = await SeedTurmaAsync(db);
        var servico = new AlunoService(db);
        var aluno = await servico.CriarAsync(new AlunoInput { Nome = "Maria" });
        var matricula = await servico.MatricularAsync(turma.Id, aluno.Id);
        await servico.AlternarAtivoMatriculaAsync(matricula);
        Assert.False(matricula.Ativa);

        var reativada = await servico.MatricularAsync(turma.Id, aluno.Id);

        Assert.True(reativada.Ativa);
        Assert.Equal(matricula.Id, reativada.Id);
    }

    [Fact]
    public void AlunoImportParser_ParseCsv_SemColunaNome_RetornaErro()
    {
        var resultado = AlunoImportParser.ParseCsv("Email,Matricula\na@b.com,123");

        Assert.Empty(resultado.Alunos);
        Assert.Single(resultado.Erros);
    }

    [Fact]
    public void AlunoImportParser_ParseCsv_LinhaValida_Reconhece()
    {
        var resultado = AlunoImportParser.ParseCsv("Nome,Email,Matricula\nMaria Silva,maria@exemplo.com,2026001\n,x@x.com,2");

        Assert.Single(resultado.Alunos);
        Assert.Equal("Maria Silva", resultado.Alunos[0].Nome);
        Assert.Single(resultado.Erros); // linha sem Nome
    }

    [Fact]
    public async Task ImportarAsync_AlunoNovo_Cria()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AlunoService(db);
        var linhas = new List<AlunoImportado> { new() { Nome = "Maria", Matricula = "2026001" } };

        var resumo = await servico.ImportarAsync(linhas, instituicaoId: null, turmaId: null);

        Assert.Equal(1, resumo.AlunosCriados);
        Assert.Equal(0, resumo.AlunosReaproveitados);
    }

    [Fact]
    public async Task ImportarAsync_MatriculaJaExistente_Reaproveita()
    {
        using var db = TestDbFactory.Criar();
        var servico = new AlunoService(db);
        await servico.CriarAsync(new AlunoInput { Nome = "Maria Antiga", Matricula = "2026001" });

        var resumo = await servico.ImportarAsync(
            new List<AlunoImportado> { new() { Nome = "Maria Silva", Matricula = "2026001" } },
            instituicaoId: null, turmaId: null);

        Assert.Equal(0, resumo.AlunosCriados);
        Assert.Equal(1, resumo.AlunosReaproveitados);
        Assert.Equal(1, (await servico.ListarTodosAsync()).Count);
    }

    [Fact]
    public async Task ImportarAsync_ComTurmaId_Matricula()
    {
        using var db = TestDbFactory.Criar();
        var turma = await SeedTurmaAsync(db);
        var servico = new AlunoService(db);

        var resumo = await servico.ImportarAsync(
            new List<AlunoImportado> { new() { Nome = "Maria" } },
            instituicaoId: null, turmaId: turma.Id);

        Assert.Equal(1, resumo.AlunosCriados);
        Assert.Equal(1, resumo.Matriculas);
        var matriculas = await servico.ListarMatriculasDaTurmaAsync(turma.Id);
        Assert.Single(matriculas);
    }
}
