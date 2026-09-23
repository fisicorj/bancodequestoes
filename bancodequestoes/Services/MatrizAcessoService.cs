using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Autorização de Matriz/Item: escopo Curso (mesma instituição, ou Admin) e
// escopo nacional Área de Curso (ENADE/DCN, escrita só Admin).
public class MatrizAcessoService(ApplicationDbContext db)
{
    public async Task<bool> TemAcessoAoCursoAsync(int cursoId, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (ehAdmin)
        {
            return true;
        }

        if (minhaInstituicaoId is null)
        {
            return false;
        }

        var instituicaoDoCurso = await db.Cursos
            .Where(c => c.Id == cursoId)
            .Select(c => (int?)c.InstituicaoId)
            .FirstOrDefaultAsync();

        return instituicaoDoCurso is not null && instituicaoDoCurso == minhaInstituicaoId;
    }

    // Mesma checagem, mas lança em vez de devolver bool — mensagem igual à
    // de "não encontrada", pra não vazar que o curso/item existe.
    public async Task GarantirAcessoAoCursoAsync(int cursoId, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await TemAcessoAoCursoAsync(cursoId, minhaInstituicaoId, ehAdmin))
        {
            throw new OperacaoInvalidaException("Curso não encontrado.");
        }
    }

    // Matrizes de escopo NACIONAL: leitura aberta a qualquer autenticado, escrita restrita a Admin.
    public static void GarantirAcessoEscritaArea(bool ehAdmin)
    {
        if (!ehAdmin)
        {
            throw new OperacaoInvalidaException(
                "Apenas administradores podem gerenciar matrizes de escopo nacional (ENADE/DCN) — elas são compartilhadas entre instituições.");
        }
    }

    // Leitura de uma matriz JÁ CARREGADA.
    public async Task<bool> TemAcessoDeLeituraAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        // Lê o Tipo, não "AreaCursoId is not null" — uma matriz nacional sem
        // Área preenchida por engano (bug real já visto) ainda é ENADE/DCN.
        if (matriz.Tipo.EhEscopoNacional())
        {
            return true;
        }

        return matriz.CursoId is int cursoId && await TemAcessoAoCursoAsync(cursoId, minhaInstituicaoId, ehAdmin);
    }

    // Escrita numa matriz JÁ CARREGADA — Curso-escopada segue a regra de
    // sempre; Área-escopada exige Admin. Lê o Tipo, mesmo raciocínio acima.
    public async Task GarantirAcessoEscritaMatrizAsync(MatrizReferencia matriz, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (matriz.Tipo.EhEscopoNacional())
        {
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await GarantirAcessoAoCursoAsync(matriz.CursoId!.Value, minhaInstituicaoId, ehAdmin);
        }
    }

    // Escopo (CursoId XOR AreaCursoId) + Tipo, buscado só com essas colunas
    // — usado quando não há a MatrizReferencia inteira já carregada.
    private async Task<(int? CursoId, int? AreaCursoId, TipoMatrizReferencia Tipo)> ObterEscopoDaMatrizOuFalharAsync(int matrizId)
    {
        var escopo = await db.MatrizesReferencia
            .Where(m => m.Id == matrizId)
            .Select(m => new { m.CursoId, m.AreaCursoId, m.Tipo })
            .FirstOrDefaultAsync()
            ?? throw new OperacaoInvalidaException("Matriz de referência não encontrada.");

        return (escopo.CursoId, escopo.AreaCursoId, escopo.Tipo);
    }

    private async Task<(int? CursoId, int? AreaCursoId, TipoMatrizReferencia Tipo)> ObterEscopoDoItemOuFalharAsync(int itemId)
    {
        var escopo = await db.ItensMatrizReferencia
            .Where(i => i.Id == itemId)
            .Select(i => new { i.MatrizReferencia!.CursoId, i.MatrizReferencia!.AreaCursoId, i.MatrizReferencia!.Tipo })
            .FirstOrDefaultAsync()
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        return (escopo.CursoId, escopo.AreaCursoId, escopo.Tipo);
    }

    private async Task GarantirAcessoEscritaEscopoAsync((int? CursoId, int? AreaCursoId, TipoMatrizReferencia Tipo) escopo, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (escopo.Tipo.EhEscopoNacional())
        {
            GarantirAcessoEscritaArea(ehAdmin);
        }
        else
        {
            await GarantirAcessoAoCursoAsync(escopo.CursoId!.Value, minhaInstituicaoId, ehAdmin);
        }
    }

    public async Task GarantirAcessoAMatrizAsync(int matrizId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var escopo = await ObterEscopoDaMatrizOuFalharAsync(matrizId);
        await GarantirAcessoEscritaEscopoAsync(escopo, minhaInstituicaoId, ehAdmin);
    }

    public async Task GarantirAcessoAoItemAsync(int itemId, int? minhaInstituicaoId, bool ehAdmin)
    {
        var escopo = await ObterEscopoDoItemOuFalharAsync(itemId);
        await GarantirAcessoEscritaEscopoAsync(escopo, minhaInstituicaoId, ehAdmin);
    }
}
