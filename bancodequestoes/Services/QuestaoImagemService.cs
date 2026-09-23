using System.Text.RegularExpressions;
using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Tudo relacionado a imagem de questão: servir o binário só se visível,
// montar/editar/remover QuestaoImagem e reescrever referências pendentes.
public class QuestaoImagemService(ApplicationDbContext db)
{
    // Serve o binário só se a questão dona for VISÍVEL pra quem pediu — senão
    // daria pra adivinhar o id na URL e ver imagens de questões privadas.
    public async Task<QuestaoImagem?> ObterVisivelAsync(int imagemId, string? meuId, int? minhaInstituicaoId)
    {
        var imagem = await db.QuestoesImagens.FindAsync(imagemId);
        if (imagem is null)
        {
            return null;
        }

        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == imagem.QuestaoId);

        return visivel ? imagem : null;
    }

    private static QuestaoImagem NovaImagemDePendente(PendenteImagem p, int ordem) => new()
    {
        NomeArquivo = p.NomeArquivo,
        ContentType = p.ContentType,
        Conteudo = p.Conteudo,
        Ordem = ordem,
        Legenda = string.IsNullOrWhiteSpace(p.Legenda) ? null : p.Legenda,
        TextoAlternativo = string.IsNullOrWhiteSpace(p.TextoAlternativo) ? null : p.TextoAlternativo,
        Alinhamento = p.Alinhamento,
        LarguraPercentual = p.LarguraPercentual,
    };

    // Monta e adiciona as QuestaoImagem novas em `destino`. Devolve só as com
    // Token preenchido, pra reescrever as referências pendentes após o SaveChanges.
    public Dictionary<string, QuestaoImagem> AdicionarPendentes(ICollection<QuestaoImagem> destino, List<PendenteImagem> imagensNovas, int ordemInicial)
    {
        var imagensPorToken = new Dictionary<string, QuestaoImagem>();
        var ordem = ordemInicial;
        foreach (var p in imagensNovas)
        {
            var novaImagem = NovaImagemDePendente(p, ordem++);
            destino.Add(novaImagem);
            if (!string.IsNullOrEmpty(p.Token))
            {
                imagensPorToken[p.Token] = novaImagem;
            }
        }
        return imagensPorToken;
    }

    // Sincroniza edições de imagens existentes por Id, sem assumir que são as mesmas instâncias rastreadas.
    public void AplicarEdicoesExistentes(Questao questao, List<QuestaoImagem> imagensExistentesEditadas)
    {
        foreach (var imgExistente in imagensExistentesEditadas)
        {
            var alvo = questao.Imagens.FirstOrDefault(i => i.Id == imgExistente.Id);
            if (alvo is not null)
            {
                alvo.Legenda = string.IsNullOrWhiteSpace(imgExistente.Legenda) ? null : imgExistente.Legenda;
                alvo.TextoAlternativo = string.IsNullOrWhiteSpace(imgExistente.TextoAlternativo) ? null : imgExistente.TextoAlternativo;
                alvo.Alinhamento = imgExistente.Alinhamento;
                alvo.LarguraPercentual = imgExistente.LarguraPercentual;
            }
        }
    }

    public void Remover(Questao questao, HashSet<int> imagensParaRemover)
    {
        foreach (var idRemover in imagensParaRemover)
        {
            var img = questao.Imagens.FirstOrDefault(i => i.Id == idRemover);
            if (img is not null)
            {
                questao.Imagens.Remove(img);
                db.QuestoesImagens.Remove(img);
            }
        }
    }

    // Troca "imagem:pendente:{token}" pela referência definitiva com o Id de
    // banco. Token sem correspondência mantém o texto original, nunca lança.
    public static string ReescreverReferenciasPendentes(string enunciado, Dictionary<string, QuestaoImagem> imagensPorToken) =>
        ReferenciaImagemPendenteRegex.Replace(enunciado, m =>
            imagensPorToken.TryGetValue(m.Groups[1].Value, out var imagem)
                ? $"imagem:existente:{imagem.Id}"
                : m.Value);

    private static readonly Regex ReferenciaImagemPendenteRegex = new(@"imagem:pendente:([A-Za-z0-9_-]+)", RegexOptions.Compiled);
}
