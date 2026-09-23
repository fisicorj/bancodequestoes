using BancoQuestoes.Data;
using BancoQuestoes.Exportacao;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Wrapper fino sobre BancoQuestoes.Exportacao — só invoca a geração e
// acrescenta a checagem de posse (provas são privadas por professor).
public class ExportacaoService(ApplicationDbContext db)
{
    public async Task<bool> EhDonoDaProvaAsync(int provaId, string? meuId)
    {
        var criadorId = await db.Provas
            .Where(p => p.Id == provaId)
            .Select(p => p.CriadoPorId)
            .FirstOrDefaultAsync();

        return criadorId is not null && criadorId == meuId;
    }

    public Task<ProvaExportDto?> CarregarAsync(int provaId) => ProvaExportLoader.CarregarAsync(db, provaId);

    public static byte[] GerarDocx(ProvaExportDto prova) => ProvaDocxExporter.Gerar(prova);

    public static byte[] GerarPdf(ProvaExportDto prova) => ProvaPdfExporter.Gerar(prova);

    public static byte[] GerarVariacoesDocx(ProvaExportDto prova, int quantidadeVersoes) => ProvaDocxExporter.GerarVariacoes(prova, quantidadeVersoes);

    public static byte[] GerarVariacoesPdf(ProvaExportDto prova, int quantidadeVersoes) => ProvaPdfExporter.GerarVariacoes(prova, quantidadeVersoes);

    // Gabarito comentado: documento à parte com resposta certa + explicação, não a prova em branco do aluno.
    public static byte[] GerarGabaritoComentadoDocx(ProvaExportDto prova) => ProvaDocxExporter.GerarGabaritoComentado(prova);

    public static byte[] GerarGabaritoComentadoPdf(ProvaExportDto prova) => ProvaPdfExporter.GerarGabaritoComentado(prova);

    // Nome de arquivo seguro pro Content-Disposition do download — movido de
    // Program.cs (era uma função local usada só pelos endpoints de exportação).
    public static string NomeArquivoSeguro(string titulo)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(titulo.Where(c => !invalidos.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(limpo) ? "prova" : limpo;
    }
}
