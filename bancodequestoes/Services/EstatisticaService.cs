using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Consultas/agrupamentos do painel de Estatisticas.razor — fica sozinho
// porque mistura contagens de Questao e Prova numa única tela.
public class EstatisticaService(ApplicationDbContext db)
{
    public async Task<PainelEstatisticas> ObterPainelAsync(string? meuId)
    {
        var minhaInstituicaoId = await db.Users.Where(u => u.Id == meuId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

        // Contagens refletem só o que este professor enxerga (VisivelPara), senão
        // o painel mostraria questões privadas de outros professores.
        var totalQuestoesAtivas = await db.Questoes.AsQueryable().VisivelPara(meuId, minhaInstituicaoId).CountAsync(q => q.Ativa);
        var totalDisciplinas = await db.Disciplinas.CountAsync();
        var totalAssuntos = await db.Assuntos.CountAsync();
        var minhasProvasTotal = await db.Provas.CountAsync(p => p.CriadoPorId == meuId);

        var inicioDoMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var provasEsteMes = await db.Provas.CountAsync(p => p.CriadoPorId == meuId && p.CriadoEm >= inicioDoMes);

        // Só entre as ativas, pra bater com o card "Questões ativas" acima.
        var grupoDisciplina = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa)
            .GroupBy(q => q.Assunto!.Disciplina!.Nome)
            .Select(g => new { Nome = g.Key, Quantidade = g.Count() })
            .ToListAsync();
        var porDisciplina = grupoDisciplina
            .Select(g => new ItemContagem { Rotulo = g.Nome, Quantidade = g.Quantidade })
            .OrderByDescending(i => i.Quantidade)
            .ToList();

        var grupoDificuldade = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa)
            .GroupBy(q => q.Dificuldade)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Quantidade);
        var porDificuldade = new List<ItemContagem>
        {
            new() { Rotulo = "Fácil", Quantidade = grupoDificuldade.GetValueOrDefault(Dificuldade.Facil) },
            new() { Rotulo = "Média", Quantidade = grupoDificuldade.GetValueOrDefault(Dificuldade.Media) },
            new() { Rotulo = "Difícil", Quantidade = grupoDificuldade.GetValueOrDefault(Dificuldade.Dificil) },
        };

        var grupoTipo = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa)
            .GroupBy(q => q.TipoQuestao)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Quantidade);
        var porTipo = new List<ItemContagem>
        {
            new() { Rotulo = "Múltipla escolha", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.MultiplaEscolha) },
            new() { Rotulo = "Discursiva", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.Discursiva) },
            new() { Rotulo = "Certo/Errado", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.CertoErrado) },
            new() { Rotulo = "Associação", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.Associacao) },
            new() { Rotulo = "Resposta breve", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.RespostaBreve) },
            new() { Rotulo = "Numérica", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.Numerica) },
            new() { Rotulo = "Preenchimento de lacunas", Quantidade = grupoTipo.GetValueOrDefault(TipoQuestao.Lacunas) },
        };

        // Monta os "baldes" dos últimos 6 meses com zero antes de somar, pra
        // um mês sem prova ainda aparecer na lista.
        var seisAtras = inicioDoMes.AddMonths(-5);
        var minhasProvasRecentes = await db.Provas
            .Where(p => p.CriadoPorId == meuId && p.CriadoEm >= seisAtras)
            .Select(p => p.CriadoEm)
            .ToListAsync();

        var meses = Enumerable.Range(0, 6).Select(i => seisAtras.AddMonths(i)).ToList();
        var provasPorMes = meses.Select(mes => new ItemContagem
        {
            Rotulo = RotuloMes(mes),
            Quantidade = minhasProvasRecentes.Count(c => c.Year == mes.Year && c.Month == mes.Month),
        }).ToList();

        return new PainelEstatisticas
        {
            TotalQuestoesAtivas = totalQuestoesAtivas,
            TotalDisciplinas = totalDisciplinas,
            TotalAssuntos = totalAssuntos,
            MinhasProvasTotal = minhasProvasTotal,
            ProvasEsteMes = provasEsteMes,
            PorDisciplina = porDisciplina,
            PorDificuldade = porDificuldade,
            PorTipo = porTipo,
            ProvasPorMes = provasPorMes,
        };
    }

    private static string RotuloMes(DateTime mes)
    {
        var nomes = new[] { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };
        return $"{nomes[mes.Month - 1]}/{mes.Year.ToString()[2..]}";
    }

    // Mapa de cobertura por Assunto (questões ativas/visíveis, por nível de
    // Bloom). Parte de TODOS os assuntos — um zerado precisa aparecer, não sumir.
    public async Task<List<CoberturaAssunto>> ObterCoberturaAsync(string? meuId)
    {
        var minhaInstituicaoId = await db.Users.Where(u => u.Id == meuId).Select(u => u.InstituicaoId).FirstOrDefaultAsync();

        var assuntos = await db.Assuntos
            .Include(a => a.Disciplina)
            .OrderBy(a => a.Disciplina!.Nome)
            .ThenBy(a => a.Nome)
            .ToListAsync();

        var questoes = await db.Questoes
            .AsQueryable()
            .VisivelPara(meuId, minhaInstituicaoId)
            .Where(q => q.Ativa)
            .Select(q => new { q.AssuntoId, q.Bloom })
            .ToListAsync();

        var porAssunto = questoes.ToLookup(q => q.AssuntoId);

        return assuntos.Select(a =>
        {
            var doAssunto = porAssunto[a.Id];
            return new CoberturaAssunto
            {
                Disciplina = a.Disciplina?.Nome ?? "?",
                Assunto = a.Nome,
                Total = doAssunto.Count(),
                Lembrar = doAssunto.Count(q => q.Bloom == NivelBloom.Lembrar),
                Entender = doAssunto.Count(q => q.Bloom == NivelBloom.Entender),
                Aplicar = doAssunto.Count(q => q.Bloom == NivelBloom.Aplicar),
                Analisar = doAssunto.Count(q => q.Bloom == NivelBloom.Analisar),
                Avaliar = doAssunto.Count(q => q.Bloom == NivelBloom.Avaliar),
                Criar = doAssunto.Count(q => q.Bloom == NivelBloom.Criar),
                SemClassificacao = doAssunto.Count(q => q.Bloom == null),
            };
        }).ToList();
    }
}
