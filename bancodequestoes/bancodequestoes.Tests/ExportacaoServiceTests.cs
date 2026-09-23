using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa ExportacaoService: EhDonoDaProvaAsync (checagem de posse — só o
// professor dono pode exportar/baixar a prova) e NomeArquivoSeguro (sanitiza
// o título pro header Content-Disposition do download).
public class ExportacaoServiceTests
{
    [Fact]
    public async Task EhDonoDaProvaAsync_CriadorConfere_RetornaTrue()
    {
        using var db = TestDbFactory.Criar();
        var prova = new Prova { Titulo = "Prova 1", CriadoPorId = "professor-a" };
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var service = new ExportacaoService(db);

        Assert.True(await service.EhDonoDaProvaAsync(prova.Id, "professor-a"));
    }

    [Fact]
    public async Task EhDonoDaProvaAsync_OutroProfessor_RetornaFalse()
    {
        using var db = TestDbFactory.Criar();
        var prova = new Prova { Titulo = "Prova 1", CriadoPorId = "professor-a" };
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var service = new ExportacaoService(db);

        Assert.False(await service.EhDonoDaProvaAsync(prova.Id, "professor-b"));
    }

    [Fact]
    public async Task EhDonoDaProvaAsync_MeuIdNulo_RetornaFalse()
    {
        using var db = TestDbFactory.Criar();
        var prova = new Prova { Titulo = "Prova 1", CriadoPorId = "professor-a" };
        db.Provas.Add(prova);
        await db.SaveChangesAsync();

        var service = new ExportacaoService(db);

        Assert.False(await service.EhDonoDaProvaAsync(prova.Id, null));
    }

    [Fact]
    public async Task EhDonoDaProvaAsync_ProvaInexistente_RetornaFalse()
    {
        using var db = TestDbFactory.Criar();
        var service = new ExportacaoService(db);

        Assert.False(await service.EhDonoDaProvaAsync(999, "professor-a"));
    }

    [Theory]
    [InlineData("Prova de Cálculo I - 2024", "Prova de Cálculo I - 2024")]
    [InlineData("Prova: Redes/Turma\"A\"", "Prova RedesTurmaA")]
    [InlineData("   ", "prova")]
    [InlineData("", "prova")]
    public void NomeArquivoSeguro_RemoveCaracteresInvalidosOuUsaFallback(string titulo, string esperado)
    {
        Assert.Equal(esperado, ExportacaoService.NomeArquivoSeguro(titulo));
    }
}
