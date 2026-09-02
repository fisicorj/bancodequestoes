using BancoQuestoes.Data;
using BancoQuestoes.Exportacao;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Wrapper fino sobre BancoQuestoes.Exportacao (ProvaExportLoader,
// ProvaDocxExporter, ProvaPdfExporter) — a lógica de carregar/gerar em si
// não é reescrita aqui, só invocada. O que este Service acrescenta é a
// checagem de posse (provas são privadas por professor) que antes vivia
// solta em Program.cs, junto dos endpoints de exportação.
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

    // Gabarito comentado: documento à parte (questão + resposta certa +
    // explicação do professor, quando preenchida) — não é a prova em branco
    // que o aluno recebe, é material de estudo/revisão ou apoio na correção.
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
