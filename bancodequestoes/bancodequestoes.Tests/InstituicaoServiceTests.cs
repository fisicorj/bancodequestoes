using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa InstituicaoService: CRUD, validação de SistemaPeriodos obrigatório
// e o tratamento de logo (definir/remover). Não cobre o catch de
// DbUpdateException na exclusão (mensagem amigável quando há Cursos
// vinculados) — o provider InMemory não aplica as mesmas restrições de FK
// do Postgres real.
public class InstituicaoServiceTests
{
    private static InstituicaoInput NovoInput(string nome = "UFMG", SistemaPeriodos? sistema = SistemaPeriodos.Semestral) => new()
    {
        Nome = nome,
        SistemaPeriodos = sistema,
    };

    [Fact]
    public async Task CriarAsync_SemSistemaPeriodos_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.CriarAsync(NovoInput(sistema: null), null));
    }

    [Fact]
    public async Task CriarAsync_ComDados_Salva()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);

        var instituicao = await service.CriarAsync(NovoInput(), null);

        Assert.NotEqual(0, instituicao.Id);
        Assert.Equal("UFMG", instituicao.Nome);
        Assert.Equal(SistemaPeriodos.Semestral, instituicao.SistemaPeriodos);
        Assert.Null(instituicao.LogoConteudo);
    }

    [Fact]
    public async Task CriarAsync_CamposOpcionaisVazios_ViramNulo()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var input = NovoInput();
        input.Endereco = "   ";
        input.Cidade = "";

        var instituicao = await service.CriarAsync(input, null);

        Assert.Null(instituicao.Endereco);
        Assert.Null(instituicao.Cidade);
    }

    [Fact]
    public async Task CriarAsync_ComLogo_SalvaConteudoEContentType()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var logo = new LogoPendente { ContentType = "image/png", Conteudo = new byte[] { 1, 2, 3 } };

        var instituicao = await service.CriarAsync(NovoInput(), logo);

        Assert.Equal("image/png", instituicao.LogoContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, instituicao.LogoConteudo);
    }

    [Fact]
    public async Task AtualizarAsync_Inexistente_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => service.AtualizarAsync(999, NovoInput(), null, false));
    }

    [Fact]
    public async Task AtualizarAsync_SemSistemaPeriodos_Lanca()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var instituicao = await service.CriarAsync(NovoInput(), null);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(
            () => service.AtualizarAsync(instituicao.Id, NovoInput(sistema: null), null, false));
    }

    [Fact]
    public async Task AtualizarAsync_RemoverLogo_LimpaConteudo()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var logo = new LogoPendente { ContentType = "image/png", Conteudo = new byte[] { 1, 2, 3 } };
        var instituicao = await service.CriarAsync(NovoInput(), logo);

        await service.AtualizarAsync(instituicao.Id, NovoInput(), null, removerLogo: true);

        var atualizada = await service.ObterAsync(instituicao.Id);
        Assert.Null(atualizada!.LogoConteudo);
        Assert.Null(atualizada.LogoContentType);
    }

    [Fact]
    public async Task AtualizarAsync_NovoLogo_Substitui()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var logoAntigo = new LogoPendente { ContentType = "image/png", Conteudo = new byte[] { 1 } };
        var instituicao = await service.CriarAsync(NovoInput(), logoAntigo);
        var logoNovo = new LogoPendente { ContentType = "image/jpeg", Conteudo = new byte[] { 9, 9 } };

        await service.AtualizarAsync(instituicao.Id, NovoInput(), logoNovo, removerLogo: false);

        var atualizada = await service.ObterAsync(instituicao.Id);
        Assert.Equal("image/jpeg", atualizada!.LogoContentType);
        Assert.Equal(new byte[] { 9, 9 }, atualizada.LogoConteudo);
    }

    [Fact]
    public async Task ExcluirAsync_SemVinculos_Remove()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        var instituicao = await service.CriarAsync(NovoInput(), null);

        await service.ExcluirAsync(instituicao);

        Assert.Null(await service.ObterAsync(instituicao.Id));
    }

    [Fact]
    public async Task ListarTodasAsync_OrdenaPorNome()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        await service.CriarAsync(NovoInput("Zeta"), null);
        await service.CriarAsync(NovoInput("Alfa"), null);

        var lista = await service.ListarTodasAsync();

        Assert.Equal(new[] { "Alfa", "Zeta" }, lista.Select(i => i.Nome));
    }

    [Fact]
    public async Task ListarAsync_SemFiltro_Pagina()
    {
        using var db = TestDbFactory.Criar();
        var service = new InstituicaoService(db);
        for (var i = 1; i <= 3; i++)
        {
            await service.CriarAsync(NovoInput($"Instituição {i}"), null);
        }

        var pagina = await service.ListarAsync(null, pagina: 1, tamanhoPagina: 2);

        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.Itens.Count);
    }
}
