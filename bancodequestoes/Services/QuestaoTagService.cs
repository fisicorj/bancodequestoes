using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Tudo relacionado a Tag de questão: vocabulário compartilhado (resolver
// nome -> Tag existente ou nova) e a tag automática "Com imagem".
public class QuestaoTagService(ApplicationDbContext db)
{
    public Task<List<Tag>> ListarTagsAsync() =>
        db.Tags.OrderBy(t => t.Nome).ToListAsync();

    // Resolve cada nome pra uma Tag existente (case-insensitive) ou cria uma nova.
    public async Task<List<Tag>> ResolverTagsAsync(List<string> nomes)
    {
        var resultado = new List<Tag>();
        foreach (var nome in nomes.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resultado.Add(await ObterOuCriarTagAsync(nome));
        }
        return resultado;
    }

    private async Task<Tag> ObterOuCriarTagAsync(string nome)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => EF.Functions.ILike(t.Nome, nome));
        if (tag is null)
        {
            tag = new Tag { Nome = nome };
            db.Tags.Add(tag);
        }
        return tag;
    }

    // Tag automática: toda questão com imagem ganha essa tag, perde quando a
    // última imagem some. Nunca mexe nas outras tags do professor.
    private const string TagComImagem = "Com imagem";

    public async Task SincronizarTagDeImagemAsync(Questao questao)
    {
        var temImagem = questao.Imagens.Count > 0;
        var tagAtual = questao.Tags.FirstOrDefault(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));

        if (temImagem && tagAtual is null)
        {
            questao.Tags.Add(await ObterOuCriarTagAsync(TagComImagem));
        }
        else if (!temImagem && tagAtual is not null)
        {
            questao.Tags.Remove(tagAtual);
        }
    }

    // Backfill idempotente: conserta questões que já tinham imagem antes
    // dessa sincronização existir. Seguro chamar toda vez que o app sobe.
    public async Task<int> SincronizarTagsDeImagemEmMassaAsync()
    {
        var questoes = await db.Questoes
            .Include(q => q.Imagens)
            .Include(q => q.Tags)
            .AsSplitQuery()
            .ToListAsync();

        var alteradas = 0;
        foreach (var questao in questoes)
        {
            var tagAntes = questao.Tags.Any(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));
            await SincronizarTagDeImagemAsync(questao);
            var tagDepois = questao.Tags.Any(t => string.Equals(t.Nome, TagComImagem, StringComparison.OrdinalIgnoreCase));

            if (tagAntes != tagDepois)
            {
                alteradas++;
            }
        }

        if (alteradas > 0)
        {
            await db.SaveChangesAsync();
        }

        return alteradas;
    }
}
