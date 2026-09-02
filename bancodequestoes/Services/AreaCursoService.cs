using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Catálogo NACIONAL de Área de Curso (2ª rodada de revisão, ver
// MatrizReferenciaService pro porquê) — CRUD simples, aberto a qualquer
// usuário autenticado, mesmo padrão pré-existente de Curso/Instituicao
// (nenhum dos dois tem dono/escopo por usuário hoje). Fica em Service
// separado de CursoService de propósito: AreaCurso não pertence a nenhuma
// Instituicao (é o oposto — várias instituições compartilham a mesma
// AreaCurso), diferente de Curso/Turma que são sempre de uma instituição.
public class AreaCursoService(ApplicationDbContext db)
{
    public Task<List<AreaCurso>> ListarTodasAsync() =>
        db.AreasCurso.OrderBy(a => a.Nome).ToListAsync();

    // Usada pelo seletor de Curso (item de matriz ENADE/DCN precisa de uma
    // AreaCurso ATIVA) e pelo formulário de Matriz — inativa fica de fora
    // dos seletores, mas continua existindo pra não quebrar matrizes já
    // vinculadas a ela.
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

    // Nome único (case-insensitive) checado aqui pra mensagem amigável — o
    // índice único no banco (ver ApplicationDbContext, NomeNormalizado) é
    // quem garante isso de verdade numa corrida entre dois cadastros
    // concorrentes.
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

    // Excluir só funciona pra área sem Cursos/Matrizes vinculados (Restrict
    // no banco) — desativar (Ativo=false) é o caminho normal pra "aposentar"
    // uma área que não deve mais aparecer nos seletores.
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
}

// "Command"/DTO da tela de Área de Curso — mesmo espírito de CursoInput.
public sealed class AreaCursoInput
{
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
