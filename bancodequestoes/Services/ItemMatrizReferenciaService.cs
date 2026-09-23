using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// CRUD de ItemMatrizReferencia (código/título/tipo/ordem/ativação);
// MatrizReferenciaService continua expondo os mesmos métodos, delegando pra cá.
public class ItemMatrizReferenciaService(ApplicationDbContext db, MatrizAcessoService acesso)
{
    public Task<List<ItemMatrizReferencia>> ListarItensAsync(int matrizId) =>
        db.ItensMatrizReferencia
            .Where(i => i.MatrizReferenciaId == matrizId)
            .OrderBy(i => i.Tipo)
                .ThenBy(i => i.Ordem)
                .ThenBy(i => i.Codigo)
            .ToListAsync();

    public Task<ItemMatrizReferencia?> ObterItemAsync(int id) =>
        db.ItensMatrizReferencia.Include(i => i.MatrizReferencia).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<ItemMatrizReferencia> CriarItemAsync(int matrizId, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        if (!await db.MatrizesReferencia.AnyAsync(m => m.Id == matrizId))
        {
            throw new OperacaoInvalidaException("Matriz de referência não encontrada.");
        }

        await acesso.GarantirAcessoAMatrizAsync(matrizId, minhaInstituicaoId, ehAdmin);
        await ValidarItemAsync(matrizId, modelo, editandoId: null);

        var item = new ItemMatrizReferencia
        {
            MatrizReferenciaId = matrizId,
            Codigo = modelo.Codigo.Trim(),
            Titulo = modelo.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao,
            Tipo = modelo.Tipo,
            Ordem = modelo.Ordem,
            Ativo = modelo.Ativo,
        };

        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task AtualizarItemAsync(int id, ItemMatrizInput modelo, int? minhaInstituicaoId, bool ehAdmin)
    {
        var item = await db.ItensMatrizReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        await acesso.GarantirAcessoAoItemAsync(id, minhaInstituicaoId, ehAdmin);
        await ValidarItemAsync(item.MatrizReferenciaId, modelo, editandoId: id);

        item.Codigo = modelo.Codigo.Trim();
        item.Titulo = modelo.Titulo.Trim();
        item.Descricao = string.IsNullOrWhiteSpace(modelo.Descricao) ? null : modelo.Descricao;
        item.Tipo = modelo.Tipo;
        item.Ordem = modelo.Ordem;
        item.Ativo = modelo.Ativo;

        await db.SaveChangesAsync();
    }

    // Código único dentro da matriz — checagem amigável; o índice único no banco garante numa corrida real.
    private async Task ValidarItemAsync(int matrizId, ItemMatrizInput modelo, int? editandoId)
    {
        if (string.IsNullOrWhiteSpace(modelo.Codigo))
        {
            throw new OperacaoInvalidaException("Informe o código do item (ex.: \"C01\").");
        }

        if (string.IsNullOrWhiteSpace(modelo.Titulo))
        {
            throw new OperacaoInvalidaException("Informe o título do item.");
        }

        var codigo = modelo.Codigo.Trim();
        var jaExiste = await db.ItensMatrizReferencia
            .AnyAsync(i => i.MatrizReferenciaId == matrizId && i.Id != (editandoId ?? 0) && EF.Functions.ILike(i.Codigo, codigo));

        if (jaExiste)
        {
            throw new OperacaoInvalidaException($"Já existe um item com o código \"{codigo}\" nessa matriz.");
        }
    }

    // Desativar em vez de excluir preserva o histórico de questões já vinculadas.
    public async Task AlternarAtivoItemAsync(int id, int? minhaInstituicaoId, bool ehAdmin)
    {
        var item = await db.ItensMatrizReferencia.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Item da matriz não encontrado.");

        await acesso.GarantirAcessoAoItemAsync(id, minhaInstituicaoId, ehAdmin);

        item.Ativo = !item.Ativo;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirItemAsync(ItemMatrizReferencia item, int? minhaInstituicaoId, bool ehAdmin)
    {
        await acesso.GarantirAcessoAoItemAsync(item.Id, minhaInstituicaoId, ehAdmin);
        try
        {
            db.ItensMatrizReferencia.Remove(item);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{item.Codigo} — {item.Titulo}\": ele ainda tem questões vinculadas. Considere desativá-lo em vez de excluir.");
        }
    }
}
