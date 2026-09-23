using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Auditoria/histórico de edição de questão: registrar o que mudou
// (RegistrarEdicaoAsync) e consultar (ObterHistoricoAsync/ListarHistoricoAsync).
public class QuestaoHistoricoService(ApplicationDbContext db)
{
    // Únicos campos que geram entrada com diff — o resto (Origem/Ano,
    // campos/coleções do subtipo) vira uma entrada genérica "Outros dados".
    private static readonly HashSet<string> CamposPrincipaisHistorico = new()
    {
        nameof(Questao.Enunciado),
        nameof(Questao.Dificuldade),
        nameof(Questao.Bloom),
        nameof(Questao.Visibilidade),
        nameof(Questao.AssuntoId),
        nameof(Questao.Ativa),
    };

    // Compara o estado atual contra o snapshot do EF Core (exige `questao`
    // rastreada). Chamado antes do SaveChangesAsync que grava a edição.
    public async Task RegistrarEdicaoAsync(Questao questao, string? meuId)
    {
        db.ChangeTracker.DetectChanges();

        var entrada = db.Entry(questao);
        var agora = DateTime.UtcNow;
        var novasEntradas = new List<QuestaoHistorico>();

        void Adicionar(string campo, string? valorAnterior, string? valorNovo) =>
            novasEntradas.Add(new QuestaoHistorico
            {
                QuestaoId = questao.Id,
                UsuarioId = meuId,
                DataHora = agora,
                Campo = campo,
                ValorAnterior = valorAnterior,
                ValorNovo = valorNovo,
            });

        // Enunciado é texto longo demais pra virar uma linha "antes → depois"
        // legível — a entrada só marca QUE mudou, sem mostrar os valores.
        if (entrada.Property(nameof(Questao.Enunciado)).IsModified)
        {
            Adicionar("Enunciado", null, null);
        }

        if (entrada.Property(nameof(Questao.Dificuldade)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Dificuldade));
            Adicionar("Dificuldade", ((Dificuldade)prop.OriginalValue!).Rotulo(), ((Dificuldade)prop.CurrentValue!).Rotulo());
        }

        if (entrada.Property(nameof(Questao.Bloom)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Bloom));
            Adicionar(
                "Nível de Bloom",
                ((NivelBloom?)prop.OriginalValue)?.Rotulo() ?? "Sem classificação",
                ((NivelBloom?)prop.CurrentValue)?.Rotulo() ?? "Sem classificação");
        }

        if (entrada.Property(nameof(Questao.Visibilidade)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Visibilidade));
            Adicionar("Visibilidade", ((VisibilidadeQuestao)prop.OriginalValue!).Rotulo(), ((VisibilidadeQuestao)prop.CurrentValue!).Rotulo());
        }

        if (entrada.Property(nameof(Questao.Ativa)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.Ativa));
            Adicionar("Status", (bool)prop.OriginalValue! ? "Ativa" : "Inativa", (bool)prop.CurrentValue! ? "Ativa" : "Inativa");
        }

        if (entrada.Property(nameof(Questao.AssuntoId)).IsModified)
        {
            var prop = entrada.Property(nameof(Questao.AssuntoId));
            var idAntigo = (int)prop.OriginalValue!;
            var idNovo = (int)prop.CurrentValue!;
            var nomes = await db.Assuntos
                .Where(a => a.Id == idAntigo || a.Id == idNovo)
                .ToDictionaryAsync(a => a.Id, a => a.Nome);
            Adicionar("Assunto", nomes.GetValueOrDefault(idAntigo, "?"), nomes.GetValueOrDefault(idNovo, "?"));
        }

        // Demais campos escalares (inclusive os do subtipo TPT) e mudanças
        // nas coleções de resposta (entidades à parte) viram UMA entrada genérica.
        var outrosCamposMudaram = entrada.Properties
            .Any(p => p.IsModified && p.Metadata.Name != nameof(Questao.AtualizadoEm) && !CamposPrincipaisHistorico.Contains(p.Metadata.Name));

        var colecoesDeRespostaMudaram = db.ChangeTracker.Entries()
            .Any(e => e.State != EntityState.Unchanged && e.Entity is AlternativaQuestao or ParAssociacao or LacunaResposta);

        if (outrosCamposMudaram || colecoesDeRespostaMudaram)
        {
            Adicionar("Outros dados da questão", null, null);
        }

        if (novasEntradas.Count > 0)
        {
            db.QuestoesHistorico.AddRange(novasEntradas);
        }
    }

    // Só quem consegue VER a questão (VisivelPara) vê seu histórico — senão
    // vazaria existência/metadados de questões privadas de outro professor.
    public async Task<List<QuestaoHistorico>> ObterHistoricoAsync(int questaoId, string? meuId, int? minhaInstituicaoId)
    {
        var visivel = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .AnyAsync(q => q.Id == questaoId);

        if (!visivel)
        {
            return new List<QuestaoHistorico>();
        }

        return await db.QuestoesHistorico
            .Include(h => h.Usuario)
            .Where(h => h.QuestaoId == questaoId)
            .OrderByDescending(h => h.DataHora)
            .ToListAsync();
    }

    // Log "geral" (HistoricoQuestoes.razor): mesma regra de VisivelPara,
    // aplicada a todas as questões via subquery de ids.
    public async Task<PaginaResultado<QuestaoHistorico>> ListarHistoricoAsync(
        HistoricoFiltro filtro, string? meuId, int? minhaInstituicaoId, int pagina, int tamanhoPagina)
    {
        var questoesVisiveis = db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId);

        if (filtro.DisciplinaId != 0)
        {
            questoesVisiveis = questoesVisiveis.Where(q => q.Assunto!.DisciplinaId == filtro.DisciplinaId);
        }

        if (filtro.AssuntoId != 0)
        {
            questoesVisiveis = questoesVisiveis.Where(q => q.AssuntoId == filtro.AssuntoId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            questoesVisiveis = questoesVisiveis.Where(q => EF.Functions.ILike(q.Enunciado, $"%{filtro.Texto}%"));
        }

        var idsVisiveis = questoesVisiveis.Select(q => q.Id);

        var query = db.QuestoesHistorico
            .Include(h => h.Usuario)
            .Include(h => h.Questao)
                .ThenInclude(q => q!.Assunto)
                    .ThenInclude(a => a!.Disciplina)
            .Where(h => idsVisiveis.Contains(h.QuestaoId));

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(h => h.DataHora)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<QuestaoHistorico> { Itens = itens, Total = total };
    }
}
