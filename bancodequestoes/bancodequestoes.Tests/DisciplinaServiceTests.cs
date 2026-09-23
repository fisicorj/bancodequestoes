using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa DisciplinaService: CRUD de Disciplina e de Assunto (a validação
// "selecione uma disciplina" e o "não encontrado"). Não cobre o catch de
// DbUpdateException nas exclusões (mensagem amigável quando há vínculos) —
// o provider InMemory não aplica as mesmas restrições de FK do Postgres real.
public class DisciplinaServiceTests
{
    [Fact]
    public async Task CriarAsync_Salva()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);

        var disciplina = await service.CriarAsync("Cálculo I", "prof-1");

        Assert.NotEqual(0, disciplina.Id);
        Assert.Equal("Cálculo I", disciplina.Nome);
        Assert.Equal("prof-1", disciplina.CriadoPorId);
        Assert.Single(await service.ListarTodasAsync());
    }

    [Fact]
    public async Task AtualizarAsync_DisciplinaExiste_AtualizaNome()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);

        await service.AtualizarAsync(disciplina.Id, "Cálculo II");

        var atualizada = await service.ObterAsync(disciplina.Id);
        Assert.Equal("Cálculo II", atualizada!.Nome);
    }

    [Fact]
    public async Task AtualizarAsync_DisciplinaInexistente_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.AtualizarAsync(999, "Novo Nome"));
    }

    [Fact]
    public async Task ExcluirAsync_SemVinculos_Remove()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);

        await service.ExcluirAsync(disciplina);

        Assert.Null(await service.ObterAsync(disciplina.Id));
    }

    [Fact]
    public async Task ListarTodasAsync_OrdenaPorNome()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        await service.CriarAsync("Zoologia", null);
        await service.CriarAsync("Álgebra", null);

        var lista = await service.ListarTodasAsync();

        Assert.Equal(new[] { "Álgebra", "Zoologia" }, lista.Select(d => d.Nome));
    }

    [Fact]
    public async Task ListarAsync_SemFiltro_Pagina()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        for (var i = 1; i <= 5; i++)
        {
            await service.CriarAsync($"Disciplina {i}", null);
        }

        var pagina1 = await service.ListarAsync(null, pagina: 1, tamanhoPagina: 2);
        var pagina2 = await service.ListarAsync(null, pagina: 2, tamanhoPagina: 2);

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.NotEqual(pagina1.Itens[0].Id, pagina2.Itens[0].Id);
    }

    // --- Assuntos ---

    [Fact]
    public async Task CriarAssuntoAsync_DisciplinaIdZero_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.CriarAssuntoAsync("Limites", 0));
    }

    [Fact]
    public async Task CriarAssuntoAsync_ComDisciplina_Salva()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);

        var assunto = await service.CriarAssuntoAsync("Limites", disciplina.Id);

        Assert.NotEqual(0, assunto.Id);
        Assert.Equal(disciplina.Id, assunto.DisciplinaId);
        Assert.Single(await service.ListarTodosAssuntosAsync());
    }

    [Fact]
    public async Task AtualizarAssuntoAsync_DisciplinaIdZero_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);
        var assunto = await service.CriarAssuntoAsync("Limites", disciplina.Id);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.AtualizarAssuntoAsync(assunto.Id, "Derivadas", 0));
    }

    [Fact]
    public async Task AtualizarAssuntoAsync_Inexistente_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.AtualizarAssuntoAsync(999, "Derivadas", disciplina.Id));
    }

    [Fact]
    public async Task AtualizarAssuntoAsync_Existente_Atualiza()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina1 = await service.CriarAsync("Cálculo I", null);
        var disciplina2 = await service.CriarAsync("Física I", null);
        var assunto = await service.CriarAssuntoAsync("Limites", disciplina1.Id);

        await service.AtualizarAssuntoAsync(assunto.Id, "Cinemática", disciplina2.Id);

        var atualizado = await service.ObterAssuntoAsync(assunto.Id);
        Assert.Equal("Cinemática", atualizado!.Nome);
        Assert.Equal(disciplina2.Id, atualizado.DisciplinaId);
    }

    [Fact]
    public async Task ExcluirAssuntoAsync_SemVinculos_Remove()
    {
        using var db = TestDbFactory.Criar();
        var service = new DisciplinaService(db);
        var disciplina = await service.CriarAsync("Cálculo I", null);
        var assunto = await service.CriarAssuntoAsync("Limites", disciplina.Id);

        await service.ExcluirAssuntoAsync(assunto);

        Assert.Null(await service.ObterAssuntoAsync(assunto.Id));
    }
}
