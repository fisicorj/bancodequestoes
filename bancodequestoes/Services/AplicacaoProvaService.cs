using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Cria e gerencia "aplicações" de uma Prova a uma Turma pra responder online — cada aluno
// matriculado (ativo) na Turma recebe um AcessoAlunoAplicacao com código PRÓPRIO, gerado no
// momento da criação; é o que o aluno digita em /responder/{codigo}, sem login.
public class AplicacaoProvaService(ApplicationDbContext db)
{
    // Sem I/O e maiúsculas confusas (0/O, 1/I) — 6 caracteres já dão ~2 bilhões de
    // combinações, mais que suficiente pra nunca colidir na prática; o loop abaixo garante
    // unicidade mesmo assim.
    private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<PaginaResultado<AplicacaoProva>> ListarAsync(int filtroProvaId, int filtroTurmaId, int pagina, int tamanhoPagina)
    {
        var query = db.AplicacoesProva
            .Include(a => a.Prova)
            .Include(a => a.Turma)
            .AsQueryable();

        if (filtroProvaId != 0)
        {
            query = query.Where(a => a.ProvaId == filtroProvaId);
        }

        if (filtroTurmaId != 0)
        {
            query = query.Where(a => a.TurmaId == filtroTurmaId);
        }

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(a => a.CriadoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<AplicacaoProva> { Itens = itens, Total = total };
    }

    public Task<AplicacaoProva?> ObterAsync(int id) =>
        db.AplicacoesProva
            .Include(a => a.Prova)
            .Include(a => a.Turma)
            .FirstOrDefaultAsync(a => a.Id == id);

    // Códigos individuais já gerados pra essa aplicação — o professor usa isso pra distribuir
    // (copiar/imprimir) um código por aluno.
    public Task<List<AcessoAlunoAplicacao>> ListarAcessosAsync(int aplicacaoId) =>
        db.AcessosAlunoAplicacao
            .Include(a => a.Aluno)
            .Where(a => a.AplicacaoProvaId == aplicacaoId)
            .OrderBy(a => a.Aluno!.Nome)
            .ToListAsync();

    // Usado pela página pública /responder/{codigo} — resolve direto qual Aluno está
    // acessando (sem precisar de dropdown pra escolher nome). Sem checar Status/DataLimite
    // aqui (isso é RespostaProvaOnlineService, que decide se pode ABRIR uma tentativa nova).
    public Task<AcessoAlunoAplicacao?> ObterAcessoPorCodigoAsync(string codigo) =>
        db.AcessosAlunoAplicacao
            .Include(a => a.Aluno)
            .Include(a => a.AplicacaoProva)
                .ThenInclude(a => a!.Prova)
            .Include(a => a.AplicacaoProva)
                .ThenInclude(a => a!.Turma)
            .FirstOrDefaultAsync(a => a.CodigoAcesso == codigo);

    public async Task<AplicacaoProva> CriarAsync(AplicacaoProvaInput modelo, string? criadoPorId)
    {
        if (modelo.ProvaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma prova.");
        }

        if (modelo.TurmaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma turma.");
        }

        if (modelo.TempoLimiteMinutos is < 1)
        {
            throw new OperacaoInvalidaException("O tempo limite, se informado, precisa ser de pelo menos 1 minuto.");
        }

        // v1 só corrige automaticamente 4 tipos (ver TiposAutoCorrigiveis) — bloquear aqui
        // evita aplicar online uma prova com Discursiva/Associação/Resposta Breve e travar
        // sem correção possível na hora de enviar.
        var tiposNaoSuportados = await db.ProvasQuestoes
            .Where(pq => pq.ProvaId == modelo.ProvaId)
            .Select(pq => pq.Questao!.TipoQuestao)
            .Where(tipo => !TiposAutoCorrigiveis.Tipos.Contains(tipo))
            .Distinct()
            .ToListAsync();

        if (tiposNaoSuportados.Count > 0)
        {
            var rotulos = string.Join(", ", tiposNaoSuportados.Select(t => t.Rotulo()));
            throw new OperacaoInvalidaException(
                $"Essa prova tem questões do tipo {rotulos}, que ainda não {(tiposNaoSuportados.Count == 1 ? "é suportado" : "são suportados")} na aplicação online (só múltipla escolha, certo/errado, numérica e lacunas).");
        }

        var alunosIds = await db.TurmasAlunos
            .Where(ta => ta.TurmaId == modelo.TurmaId && ta.Ativa)
            .Select(ta => ta.AlunoId)
            .ToListAsync();

        if (alunosIds.Count == 0)
        {
            throw new OperacaoInvalidaException("Essa turma não tem nenhum aluno matriculado ativo — matricule os alunos antes de aplicar a prova online.");
        }

        var aplicacao = new AplicacaoProva
        {
            ProvaId = modelo.ProvaId,
            TurmaId = modelo.TurmaId,
            DataLimite = modelo.DataLimite,
            TempoLimiteMinutos = modelo.TempoLimiteMinutos,
            CriadoPorId = criadoPorId,
        };
        db.AplicacoesProva.Add(aplicacao);
        await db.SaveChangesAsync();

        // Um código próprio por aluno matriculado — gerado de uma vez só aqui; um aluno
        // matriculado depois precisa ser adicionado manualmente (ver GerarAcessoAsync).
        foreach (var alunoId in alunosIds)
        {
            db.AcessosAlunoAplicacao.Add(new AcessoAlunoAplicacao
            {
                AplicacaoProvaId = aplicacao.Id,
                AlunoId = alunoId,
                CodigoAcesso = await GerarCodigoUnicoAsync(),
            });
        }
        await db.SaveChangesAsync();

        return aplicacao;
    }

    // Gera o código individual de um aluno matriculado DEPOIS da aplicação já ter sido
    // criada (os demais já ganharam o deles em CriarAsync) — idempotente: se já existe,
    // devolve o mesmo em vez de duplicar.
    public async Task<AcessoAlunoAplicacao> GerarAcessoAsync(int aplicacaoId, int alunoId)
    {
        var existente = await db.AcessosAlunoAplicacao
            .FirstOrDefaultAsync(a => a.AplicacaoProvaId == aplicacaoId && a.AlunoId == alunoId);
        if (existente is not null)
        {
            return existente;
        }

        var acesso = new AcessoAlunoAplicacao
        {
            AplicacaoProvaId = aplicacaoId,
            AlunoId = alunoId,
            CodigoAcesso = await GerarCodigoUnicoAsync(),
        };
        db.AcessosAlunoAplicacao.Add(acesso);
        await db.SaveChangesAsync();
        return acesso;
    }

    // Encerrar bloqueia novos acessos mesmo dentro do prazo; reabrir desfaz isso — tentativas
    // já Enviadas/Liberadas não são afetadas, só a possibilidade de começar uma nova.
    public async Task AlternarStatusAsync(AplicacaoProva aplicacao)
    {
        aplicacao.Status = aplicacao.Status == StatusAplicacaoProva.Aberta
            ? StatusAplicacaoProva.Encerrada
            : StatusAplicacaoProva.Aberta;
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(AplicacaoProva aplicacao)
    {
        try
        {
            db.AplicacoesProva.Remove(aplicacao);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException(
                "Não foi possível excluir essa aplicação: já existem tentativas de aluno vinculadas a ela. Encerre-a em vez de excluir.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("severed", StringComparison.OrdinalIgnoreCase))
        {
            // Mesmo caso do DbUpdateException acima, só que detectado pelo EF ANTES de
            // bater no banco: acontece quando as RespostaProvaOnline dessa aplicação já
            // estavam rastreadas neste DbContext (ex.: usuário visitou a tela de
            // resultados antes de tentar excluir) — o Restrict de RespostaProvaOnline
            // vira essa exceção em vez de DbUpdateException nesse cenário específico.
            throw new OperacaoInvalidaException(
                "Não foi possível excluir essa aplicação: já existem tentativas de aluno vinculadas a ela. Encerre-a em vez de excluir.");
        }
    }

    private async Task<string> GerarCodigoUnicoAsync()
    {
        for (var tentativa = 0; tentativa < 20; tentativa++)
        {
            var codigo = GerarCodigoAleatorio();
            if (!await db.AcessosAlunoAplicacao.AnyAsync(a => a.CodigoAcesso == codigo))
            {
                return codigo;
            }
        }

        throw new OperacaoInvalidaException("Não foi possível gerar um código de acesso único — tente novamente.");
    }

    private static string GerarCodigoAleatorio() =>
        new(Enumerable.Range(0, 6).Select(_ => AlfabetoCodigo[Random.Shared.Next(AlfabetoCodigo.Length)]).ToArray());
}
