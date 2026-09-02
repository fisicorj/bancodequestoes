using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Mesmo padrão do DisciplinaService: consulta EF + regra de negócio de
// Instituição (incluindo upload/remoção de logo). InstituicaoList.razor e
// InstituicaoForm.razor ficam só com UI + binding.
public class InstituicaoService(ApplicationDbContext db)
{
    public async Task<PaginaResultado<Instituicao>> ListarAsync(string? filtroTexto, int pagina, int tamanhoPagina)
    {
        var query = db.Instituicoes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(i => EF.Functions.ILike(i.Nome, $"%{filtroTexto}%")
                || (i.Cidade != null && EF.Functions.ILike(i.Cidade, $"%{filtroTexto}%")));
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderBy(i => i.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<Instituicao> { Itens = itens, Total = total };
    }

    // Usado pelos seletores (Curso, Perfil) que precisam de "todas as
    // instituições", sem paginação.
    public Task<List<Instituicao>> ListarTodasAsync() =>
        db.Instituicoes.OrderBy(i => i.Nome).ToListAsync();

    public Task<Instituicao?> ObterAsync(int id) => db.Instituicoes.FindAsync(id).AsTask();

    public async Task<Instituicao> CriarAsync(InstituicaoInput modelo, LogoPendente? logoNova)
    {
        ValidarSistemaPeriodos(modelo);

        var instituicao = new Instituicao
        {
            Nome = modelo.Nome,
            Endereco = NuloSeVazio(modelo.Endereco),
            Cidade = NuloSeVazio(modelo.Cidade),
            Telefone = NuloSeVazio(modelo.Telefone),
            Site = NuloSeVazio(modelo.Site),
            Instrucoes = NuloSeVazio(modelo.Instrucoes),
            SistemaPeriodos = modelo.SistemaPeriodos!.Value,
        };

        if (logoNova is not null)
        {
            instituicao.LogoConteudo = logoNova.Conteudo;
            instituicao.LogoContentType = logoNova.ContentType;
        }

        db.Instituicoes.Add(instituicao);
        await db.SaveChangesAsync();
        return instituicao;
    }

    public async Task AtualizarAsync(int id, InstituicaoInput modelo, LogoPendente? logoNova, bool removerLogo)
    {
        ValidarSistemaPeriodos(modelo);

        var instituicao = await db.Instituicoes.FindAsync(id)
            ?? throw new OperacaoInvalidaException("Instituição não encontrada.");

        instituicao.Nome = modelo.Nome;
        instituicao.Endereco = NuloSeVazio(modelo.Endereco);
        instituicao.Cidade = NuloSeVazio(modelo.Cidade);
        instituicao.Telefone = NuloSeVazio(modelo.Telefone);
        instituicao.Site = NuloSeVazio(modelo.Site);
        instituicao.Instrucoes = NuloSeVazio(modelo.Instrucoes);
        instituicao.SistemaPeriodos = modelo.SistemaPeriodos!.Value;

        if (logoNova is not null)
        {
            instituicao.LogoConteudo = logoNova.Conteudo;
            instituicao.LogoContentType = logoNova.ContentType;
        }
        else if (removerLogo)
        {
            instituicao.LogoConteudo = null;
            instituicao.LogoContentType = null;
        }

        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Instituicao instituicao)
    {
        try
        {
            db.Instituicoes.Remove(instituicao);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                $"Não foi possível excluir \"{instituicao.Nome}\": verifique se ela ainda tem Cursos vinculados.");
        }
    }

    private static string? NuloSeVazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    // Defesa em profundidade: o DataAnnotationsValidator do EditForm já
    // bloqueia o submit sem escolher o sistema de períodos, mas o Service não
    // pode depender só disso — mesmo padrão do resto do sistema (validar de
    // novo aqui, não confiar só na tela).
    private static void ValidarSistemaPeriodos(InstituicaoInput modelo)
    {
        if (modelo.SistemaPeriodos is null)
        {
            throw new OperacaoInvalidaException("Escolha o sistema de períodos da instituição.");
        }
    }
}
