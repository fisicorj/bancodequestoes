using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Primeiro Service do sistema — estabelece o padrão que os próximos
// (QuestaoService, ProvaService...) vão seguir: injeta o ApplicationDbContext
// (Scoped, igual o Service — mesma instância dura o circuito Blazor inteiro),
// concentra consulta EF + regra de negócio, e sinaliza erro "de negócio" via
// OperacaoInvalidaException em vez de deixar DbUpdateException/null vazar pra
// tela. As páginas .razor de Disciplinas/Assuntos ficam só com UI + binding.
//
// Disciplina e Assunto moram no mesmo Service de propósito: Assunto não faz
// sentido sem uma Disciplina (é sempre filho dela), então trata-los como um
// único agregado evita fragmentar demais a camada de Services.
public class DisciplinaService(ApplicationDbContext db)
{
    public async Task<PaginaResultado<Disciplina>> ListarAsync(string? filtroTexto, int pagina, int tamanhoPagina)
    {
        var query = db.Disciplinas.Include(d => d.Assuntos).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(d => EF.Functions.ILike(d.Nome, $"%{filtroTexto}%"));
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderBy(d => d.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Disciplina> { Itens = itens, Total = total };
    }

    // Usado pelos seletores (dropdown) de outras telas — Assunto, Prova,
    // Questão etc. — que precisam de "todas as disciplinas", sem paginação.
    public Task<List<Disciplina>> ListarTodasAsync() =>
        db.Disciplinas.OrderBy(d => d.Nome).ToListAsync();

    public Task<Disciplina?> ObterAsync(int id) => db.Disciplinas.FindAsync(id).AsTask();

    public async Task<Disciplina> CriarAsync(string nome, string? criadoPorId)
    {
        var disciplina = new Disciplina { Nome = nome, CriadoPorId = criadoPorId };
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        return disciplina;
    }

    public async Task AtualizarAsync(int id, string nome)
    {
        var disciplina = await db.Disciplinas.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Disciplina não encontrada.");

        disciplina.Nome = nome;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Disciplina disciplina)
    {
        try
        {
            db.Disciplinas.Remove(disciplina);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Assuntos/Questões vinculados podem impedir a exclusão — mensagem
            // amigável em vez da exceção crua do Postgres/EF.
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{disciplina.Nome}\": verifique se ela ainda tem Assuntos vinculados.");
        }
    }

    // --- Assuntos ---

    public async Task<PaginaResultado<Assunto>> ListarAssuntosAsync(string? filtroTexto, int filtroDisciplinaId, int pagina, int tamanhoPagina)
    {
        var query = db.Assuntos
            .Include(a => a.Disciplina)
            .Include(a => a.Questoes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(a => EF.Functions.ILike(a.Nome, $"%{filtroTexto}%"));
        }

        if (filtroDisciplinaId != 0)
        {
            query = query.Where(a => a.DisciplinaId == filtroDisciplinaId);
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderBy(a => a.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Assunto> { Itens = itens, Total = total };
    }

    // Usado pelos seletores de outras telas (Questão, Prova) que precisam de
    // "todos os assuntos" com a Disciplina já carregada, sem paginação.
    public Task<List<Assunto>> ListarTodosAssuntosAsync() =>
        db.Assuntos.Include(a => a.Disciplina).OrderBy(a => a.Nome).ToListAsync();

    public Task<Assunto?> ObterAssuntoAsync(int id) => db.Assuntos.FindAsync(id).AsTask();

    public async Task<Assunto> CriarAssuntoAsync(string nome, int disciplinaId)
    {
        if (disciplinaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma disciplina.");
        }

        var assunto = new Assunto { Nome = nome, DisciplinaId = disciplinaId };
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return assunto;
    }

    public async Task AtualizarAssuntoAsync(int id, string nome, int disciplinaId)
    {
        if (disciplinaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma disciplina.");
        }

        var assunto = await db.Assuntos.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Assunto não encontrado.");

        assunto.Nome = nome;
        assunto.DisciplinaId = disciplinaId;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAssuntoAsync(Assunto assunto)
    {
        try
        {
            db.Assuntos.Remove(assunto);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{assunto.Nome}\": verifique se ele ainda tem Questões vinculadas.");
        }
    }
}
