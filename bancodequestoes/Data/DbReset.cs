using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Data;

// Só disparado com "dotnet run -- --resetar-questoes" (ver Program.cs); nunca
// sozinho no "dotnet run" normal, diferente dos seeds (sempre seguros de rodar).
public static class DbReset
{
    // Ordem importa (Prova -> Questao, FK Restrict); tudo numa transação, pra
    // não deixar o banco pela metade se algo falhar.
    public static async Task ApagarTodasProvasEQuestoesAsync(ApplicationDbContext db)
    {
        var totalProvas = await db.Provas.CountAsync();
        var totalQuestoes = await db.Questoes.CountAsync();

        Console.WriteLine($"[DbReset] Apagando {totalProvas} prova(s) e {totalQuestoes} questão(ões) existentes...");

        await using var transacao = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Provas\"");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Questoes\"");
            await transacao.CommitAsync();
        }
        catch
        {
            await transacao.RollbackAsync();
            throw;
        }

        Console.WriteLine("[DbReset] Concluído — Provas e Questões zeradas. Disciplinas/Assuntos/Cursos/Matrizes foram preservados.");
    }
}
