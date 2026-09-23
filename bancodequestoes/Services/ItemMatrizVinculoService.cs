using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Validação do vínculo Questão <-> Item de Matriz, usada por
// QuestaoCurricularService e QuestaoForm.razor. Só leitura.
public class ItemMatrizVinculoService(ApplicationDbContext db)
{
    // Itens ATIVOS de matrizes institucionais do curso — nacionais (ENADE/DCN) só via a versão por Área abaixo.
    public async Task<List<ItemMatrizReferencia>> ListarItensVinculaveisPorCursoAsync(int cursoId)
    {
        return await db.ItensMatrizReferencia
            .Include(i => i.MatrizReferencia)
            .Where(i => i.MatrizReferencia!.CursoId == cursoId
                && i.MatrizReferencia.Status != StatusMatrizReferencia.Rascunho
                && i.Ativo)
            .OrderBy(i => i.MatrizReferencia!.Tipo)
                .ThenByDescending(i => i.MatrizReferencia!.Ano)
                .ThenBy(i => i.Tipo)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();
    }

    // Contraparte por ÁREA de Curso — PPC/Institucional/Outro nunca aparecem aqui.
    public async Task<List<ItemMatrizReferencia>> ListarItensVinculaveisPorAreasCursoAsync(IEnumerable<int> areaCursoIds)
    {
        var ids = areaCursoIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        return await db.ItensMatrizReferencia
            .Include(i => i.MatrizReferencia)
            .Where(i => i.MatrizReferencia!.AreaCursoId != null
                && ids.Contains(i.MatrizReferencia.AreaCursoId!.Value)
                && i.MatrizReferencia.Status != StatusMatrizReferencia.Rascunho
                && i.Ativo)
            .OrderBy(i => i.MatrizReferencia!.Tipo)
                .ThenByDescending(i => i.MatrizReferencia!.Ano)
                .ThenBy(i => i.Tipo)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();
    }

    // Itens da matriz ENADE VIGENTE de cada Área, só pra sugestão via IA (edição superada não serve).
    public async Task<List<ItemMatrizReferencia>> ListarItensEnadeAtivosAsync()
    {
        // Defensivo: dados antigos podem ter 2 matrizes Ativas por Área — fica só com a mais recente.
        var matrizesAtivasPorArea = await db.MatrizesReferencia
            .Where(m => m.Tipo == TipoMatrizReferencia.ENADE
                && m.AreaCursoId != null
                && m.Status == StatusMatrizReferencia.Ativa)
            .ToListAsync();

        var matrizIdsValidas = matrizesAtivasPorArea
            .GroupBy(m => m.AreaCursoId!.Value)
            .Select(g => g.OrderByDescending(m => m.Ano).ThenByDescending(m => m.Id).First().Id)
            .ToHashSet();

        if (matrizIdsValidas.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        return await db.ItensMatrizReferencia
            .Include(i => i.MatrizReferencia!)
                .ThenInclude(m => m.AreaCurso)
            .Where(i => matrizIdsValidas.Contains(i.MatrizReferenciaId) && i.Ativo)
            .OrderBy(i => i.MatrizReferencia!.AreaCurso!.Nome)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();
    }

    // Contraparte de ValidarItensDoCursoAsync pela AreaCurso — usadas juntas (união) por QuestaoCurricularService.
    public async Task<List<ItemMatrizReferencia>> ValidarItensPorAreasCursoAsync(IEnumerable<int> areaCursoIds, IEnumerable<int> itemIds)
    {
        var ids = itemIds.ToList();
        var areaIds = areaCursoIds.Distinct().ToList();
        if (ids.Count == 0 || areaIds.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        return await db.ItensMatrizReferencia
            .Where(i => i.MatrizReferencia!.AreaCursoId != null
                && areaIds.Contains(i.MatrizReferencia.AreaCursoId!.Value)
                && i.MatrizReferencia.Status != StatusMatrizReferencia.Rascunho
                && i.Ativo
                && ids.Contains(i.Id))
            .ToListAsync();
    }

    // Valida só matrizes institucionais — Curso nunca concede acesso implícito às matrizes nacionais da sua Área.
    public async Task<List<ItemMatrizReferencia>> ValidarItensDoCursoAsync(int? cursoId, IEnumerable<int> itemIds)
    {
        var ids = itemIds.ToList();
        if (cursoId is null || ids.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        return await db.ItensMatrizReferencia
            .Where(i => i.MatrizReferencia!.CursoId == cursoId.Value
                && i.MatrizReferencia.Status != StatusMatrizReferencia.Rascunho
                && i.Ativo
                && ids.Contains(i.Id))
            .ToListAsync();
    }
}
