using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Catálogo NACIONAL de Área de Curso — CRUD simples, sem dono/escopo por usuário.
// Service separado de CursoService porque AreaCurso é compartilhada entre instituições.
public class AreaCursoService(ApplicationDbContext db)
{
    public Task<List<AreaCurso>> ListarTodasAsync() =>
        db.AreasCurso.OrderBy(a => a.Nome).ToListAsync();

    // Inativa fica de fora dos seletores, mas continua existindo pra não quebrar matrizes já vinculadas.
    public Task<List<AreaCurso>> ListarAtivasAsync() =>
        db.AreasCurso.Where(a => a.Ativo).OrderBy(a => a.Nome).ToListAsync();

    public Task<AreaCurso?> ObterAsync(int id) => db.AreasCurso.FindAsync(id).AsTask();

    public async Task<AreaCurso> CriarAsync(AreaCursoInput modelo)
    {
        await ValidarAsync(modelo, editandoId: null);

        var area = new AreaCurso { Nome = modelo.Nome.Trim(), Ativo = modelo.Ativo };
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();
        return area;
    }

    public async Task AtualizarAsync(int id, AreaCursoInput modelo)
    {
        await ValidarAsync(modelo, editandoId: id);

        var area = await db.AreasCurso.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Área de curso não encontrada.");

        area.Nome = modelo.Nome.Trim();
        area.Ativo = modelo.Ativo;
        await db.SaveChangesAsync();
    }

    // Checagem aqui é só pra mensagem amigável — o índice único no banco garante numa corrida real.
    private async Task ValidarAsync(AreaCursoInput modelo, int? editandoId)
    {
        if (string.IsNullOrWhiteSpace(modelo.Nome))
        {
            throw new OperacaoInvalidaException("Informe o nome da área de curso (ex.: \"Engenharia de Computação\").");
        }

        var nome = modelo.Nome.Trim();
        var jaExiste = await db.AreasCurso
            .AnyAsync(a => a.Id != (editandoId ?? 0) && EF.Functions.ILike(a.Nome, nome));

        if (jaExiste)
        {
            throw new OperacaoInvalidaException($"Já existe uma área de curso chamada \"{nome}\".");
        }
    }

    // Excluir só funciona sem Cursos/Matrizes vinculados — desativar é o caminho normal pra "aposentar" uma área.
    public async Task ExcluirAsync(AreaCurso area)
    {
        try
        {
            db.AreasCurso.Remove(area);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{area.Nome}\": ainda existem cursos ou matrizes vinculados a ela. Considere desativá-la em vez de excluir.");
        }
    }

    
    public async Task<Dictionary<int, EstatisticasAreaCurso>> ObterEstatisticasAsync()
    {
        var porCurso = await db.Cursos
            .Where(c => c.AreaCursoId != null)
            .GroupBy(c => c.AreaCursoId!.Value)
            .Select(g => new { AreaCursoId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.AreaCursoId, x => x.Total);

        var porMatriz = await db.MatrizesReferencia
            .Where(m => m.AreaCursoId != null)
            .GroupBy(m => m.AreaCursoId!.Value)
            .Select(g => new { AreaCursoId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.AreaCursoId, x => x.Total);

        var porQuestao = await db.QuestoesAreasCurso
            .GroupBy(qa => qa.AreaCursoId)
            .Select(g => new { AreaCursoId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.AreaCursoId, x => x.Total);

        var todasIds = await db.AreasCurso.Select(a => a.Id).ToListAsync();

        return todasIds.ToDictionary(id => id, id => new EstatisticasAreaCurso
        {
            Cursos = porCurso.GetValueOrDefault(id),
            Matrizes = porMatriz.GetValueOrDefault(id),
            Questoes = porQuestao.GetValueOrDefault(id),
        });
    }

    
    public async Task<ResultadoMesclagemAreaCurso> MesclarAsync(int origemId, int destinoId, bool ehAdmin)
    {
        if (!ehAdmin)
        {
            throw new OperacaoInvalidaException(
                "Apenas administradores podem mesclar áreas de curso — a operação reatribui dados de várias instituições de uma vez.");
        }

        if (origemId == destinoId)
        {
            throw new OperacaoInvalidaException("Selecione duas áreas de curso diferentes para mesclar.");
        }

        var origem = await db.AreasCurso.FindAsync(origemId)
            ?? throw new OperacaoInvalidaException("Área de curso de origem não encontrada.");
        var destino = await db.AreasCurso.FindAsync(destinoId)
            ?? throw new OperacaoInvalidaException("Área de curso de destino não encontrada.");

        using var transacao = await db.Database.BeginTransactionAsync();

        
        var cursosParaMover = await db.Cursos.Where(c => c.AreaCursoId == origemId).ToListAsync();
        foreach (var curso in cursosParaMover)
        {
            curso.AreaCursoId = destinoId;
        }
        var cursosMovidos = cursosParaMover.Count;

        var matrizesParaMover = await db.MatrizesReferencia.Where(m => m.AreaCursoId == origemId).ToListAsync();
        foreach (var matriz in matrizesParaMover)
        {
            matriz.AreaCursoId = destinoId;
        }
        var matrizesMovidas = matrizesParaMover.Count;

        await db.SaveChangesAsync();

        
        var vinculosOrigem = await db.QuestoesAreasCurso.Where(qa => qa.AreaCursoId == origemId).ToListAsync();
        var idsQuestaoNoDestino = await db.QuestoesAreasCurso
            .Where(qa => qa.AreaCursoId == destinoId)
            .Select(qa => qa.QuestaoId)
            .ToHashSetAsync();

        var questoesMovidas = 0;
        foreach (var vinculo in vinculosOrigem)
        {
            if (idsQuestaoNoDestino.Contains(vinculo.QuestaoId))
            {
                db.QuestoesAreasCurso.Remove(vinculo);
            }
            else
            {
                db.QuestoesAreasCurso.Remove(vinculo);
                db.QuestoesAreasCurso.Add(new QuestaoAreaCurso { QuestaoId = vinculo.QuestaoId, AreaCursoId = destinoId });
                questoesMovidas++;
            }
        }
        await db.SaveChangesAsync();

        // Diagnóstico não-bloqueante: reporta se sobrou mais de uma Matriz ENADE Ativa no destino, sem decidir sozinho.
        var enadeAtivasNoDestino = await db.MatrizesReferencia.CountAsync(m =>
            m.AreaCursoId == destinoId && m.Tipo == TipoMatrizReferencia.ENADE && m.Status == StatusMatrizReferencia.Ativa);

        db.AreasCurso.Remove(origem);
        await db.SaveChangesAsync();

        await transacao.CommitAsync();

        return new ResultadoMesclagemAreaCurso
        {
            NomeOrigem = origem.Nome,
            NomeDestino = destino.Nome,
            CursosMovidos = cursosMovidos,
            MatrizesMovidas = matrizesMovidas,
            QuestoesMovidas = questoesMovidas,
            RevisarMatrizesEnadeAtivasNoDestino = enadeAtivasNoDestino > 1,
        };
    }
}

// Contagens de uso de uma AreaCurso — pista de duplicidade quando duas linhas parecidas têm contagens não-zero.
public sealed class EstatisticasAreaCurso
{
    public int Cursos { get; init; }
    public int Matrizes { get; init; }
    public int Questoes { get; init; }
}

// Resultado da mesclagem — o que a tela mostra depois de confirmar.
public sealed class ResultadoMesclagemAreaCurso
{
    public required string NomeOrigem { get; init; }
    public required string NomeDestino { get; init; }
    public int CursosMovidos { get; init; }
    public int MatrizesMovidas { get; init; }
    public int QuestoesMovidas { get; init; }
    public bool RevisarMatrizesEnadeAtivasNoDestino { get; init; }
}

// "Command"/DTO da tela de Área de Curso
public sealed class AreaCursoInput
{
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
