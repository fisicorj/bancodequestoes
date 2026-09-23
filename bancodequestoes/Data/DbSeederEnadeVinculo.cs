using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Data;

// Vínculo real entre questões seedadas e Itens de Matriz ENADE/DCN, por
// palavra-chave (não por código fixo); opcional, nunca falha o seed.
public static class DbSeederEnadeVinculo
{
    // Cada Disciplina seedada mapeada pra palavras plausíveis no Título/
    // Descrição de um Item ENADE de Eng. da Computação (Portaria 279/2023).
    private static readonly Dictionary<string, string[]> PalavrasChavePorDisciplina = new()
    {
        ["Programação"] = new[] { "algoritmo", "estrutura de dados", "programação", "linguagens de programação" },
        ["Banco de Dados"] = new[] { "banco de dados", "dados" },
        ["Redes de Computadores"] = new[] { "rede" },
        ["Sistemas Operacionais"] = new[] { "sistema operacional", "sistemas operacionais" },
        ["Arquitetura de Computadores"] = new[] { "arquitetura", "organização de computadores" },
        ["Engenharia de Software"] = new[] { "engenharia de software", "software" },
        ["Segurança da Informação"] = new[] { "segurança" },
        ["Matemática"] = new[] { "matemática", "cálculo", "álgebra", "estatística", "probabilidade" },
    };

    // Só vincula uma fração (1 a cada 3), pra não lotar um único Item com
    // dezenas de questões.
    private const int IntervaloDeVinculo = 3;

    public static async Task VincularAsync(ApplicationDbContext db, string nomeCurso)
    {
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Nome == nomeCurso);
        if (curso is null)
        {
            Console.WriteLine($"[EnadeVinculo] Curso \"{nomeCurso}\" não encontrado — questões seedadas ficam sem vínculo formal de Matriz (só com os temas/Disciplinas alinhados ao ENADE).");
            return;
        }

        var itens = curso.AreaCursoId is int areaCursoId
            ? await db.ItensMatrizReferencia
                .Include(i => i.MatrizReferencia)
                .Where(i => i.Ativo
                    && i.MatrizReferencia!.AreaCursoId == areaCursoId
                    && (i.MatrizReferencia.Tipo == TipoMatrizReferencia.ENADE || i.MatrizReferencia.Tipo == TipoMatrizReferencia.DCN))
                .ToListAsync()
            : await db.ItensMatrizReferencia
                .Include(i => i.MatrizReferencia)
                .Where(i => i.Ativo
                    && i.MatrizReferencia!.CursoId == curso.Id
                    && (i.MatrizReferencia.Tipo == TipoMatrizReferencia.ENADE || i.MatrizReferencia.Tipo == TipoMatrizReferencia.DCN))
                .ToListAsync();

        if (itens.Count == 0)
        {
            Console.WriteLine($"[EnadeVinculo] \"{nomeCurso}\" existe, mas não tem nenhum Item de Matriz ENADE/DCN ativo (matriz ainda não importada, ou não ativa) — questões seedadas ficam sem vínculo formal de Matriz.");
            return;
        }

        // Carregada uma vez fora do laço, pra vincular via QuestaoAreaCurso
        // (fonte de aplicabilidade acadêmica); sem AreaCursoId, fica só o vínculo de Item.
        var areaCurso = curso.AreaCursoId is int areaId
            ? await db.AreasCurso.FirstOrDefaultAsync(a => a.Id == areaId)
            : null;

        var questoes = await db.Questoes
            .Include(q => q.Assunto)
            .ThenInclude(a => a!.Disciplina)
            .Include(q => q.ItensMatriz)
            .Include(q => q.AreasCurso)
            .Where(q => PalavrasChavePorDisciplina.Keys.Contains(q.Assunto!.Disciplina!.Nome))
            .OrderBy(q => q.Id)
            .ToListAsync();

        var elegivelPorDisciplina = new Dictionary<string, int>();
        var vinculadas = 0;

        foreach (var questao in questoes)
        {
            if (questao.ItensMatriz.Count > 0)
            {
                continue; // já vinculada (rodada anterior deste seeder) — idempotente.
            }

            var disciplinaNome = questao.Assunto!.Disciplina!.Nome;
            if (!PalavrasChavePorDisciplina.TryGetValue(disciplinaNome, out var palavras))
            {
                continue;
            }

            elegivelPorDisciplina.TryGetValue(disciplinaNome, out var contador);
            elegivelPorDisciplina[disciplinaNome] = contador + 1;
            if (contador % IntervaloDeVinculo != 0)
            {
                continue; // fora do intervalo de amostragem (ver IntervaloDeVinculo).
            }

            var item = itens.FirstOrDefault(i =>
                palavras.Any(p => i.Titulo.Contains(p, StringComparison.OrdinalIgnoreCase))
                || (i.Descricao != null && palavras.Any(p => i.Descricao.Contains(p, StringComparison.OrdinalIgnoreCase))));
            if (item is null)
            {
                continue;
            }

            if (areaCurso is not null && !questao.AreasCurso.Any(a => a.Id == areaCurso.Id))
            {
                questao.AreasCurso.Add(areaCurso);
            }
            questao.ItensMatriz.Add(item);
            vinculadas++;
        }

        if (vinculadas > 0)
        {
            await db.SaveChangesAsync();
        }

        Console.WriteLine($"[EnadeVinculo] {vinculadas} questão(ões) vinculada(s) a Item(ns) de Matriz ENADE/DCN de \"{nomeCurso}\" (de {itens.Count} item(ns) ativo(s) disponível(eis)).");
    }
}
