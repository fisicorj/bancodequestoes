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

    // Aplicações são privadas por professor, mesmo critério de Prova (item 18) — sem esse
    // filtro, qualquer professor logado listava/via aplicações de outro, inclusive notas e
    // códigos de acesso de alunos de outra turma/instituição (IDOR corrigido aqui).
    public async Task<PaginaResultado<AplicacaoProva>> ListarAsync(string? meuId, int filtroProvaId, int filtroTurmaId, int pagina, int tamanhoPagina)
    {
        var query = db.AplicacoesProva
            .Include(a => a.Prova)
            .Include(a => a.Turma)
            .Where(a => a.CriadoPorId == meuId)
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

    // Checagem de posse (mesmo padrão de ExportacaoService.EhDonoDaProvaAsync) — usada tanto
    // pelas páginas de resultados/códigos quanto pelos endpoints de download em Program.cs.
    public async Task<bool> EhDonoAsync(int aplicacaoId, string? meuId)
    {
        var criadorId = await db.AplicacoesProva
            .Where(a => a.Id == aplicacaoId)
            .Select(a => a.CriadoPorId)
            .FirstOrDefaultAsync();

        return criadorId is not null && criadorId == meuId;
    }

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

        // Achado médio da auditoria: o dropdown do formulário só lista provas do próprio
        // professor, mas isso é só front-end — sem essa checagem no Service, um ProvaId de
        // outro professor passava direto e a aplicação online expunha o enunciado/gabarito
        // alheio pros alunos via /responder (mesmo critério de posse do item 18).
        var provaEhDoProfessor = await db.Provas.AnyAsync(p => p.Id == modelo.ProvaId && p.CriadoPorId == criadoPorId);
        if (!provaEhDoProfessor)
        {
            throw new OperacaoInvalidaException("Prova não encontrada.");
        }

        if (modelo.TempoLimiteMinutos is < 1)
        {
            throw new OperacaoInvalidaException("O tempo limite, se informado, precisa ser de pelo menos 1 minuto.");
        }

        // Achado baixo da auditoria: nada impedia criar a aplicação já com o prazo no
        // passado — a prova nascia encerrada (RespostaProvaOnlineService.IniciarOuRetomarAsync
        // rejeita de cara "prazo já passou"), sem nenhum aviso na hora de criar.
        if (modelo.DataLimite is { } dataLimite && dataLimite <= DateTime.UtcNow)
        {
            throw new OperacaoInvalidaException("O prazo final precisa ser no futuro.");
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
        const string mensagemBloqueio =
            "Não foi possível excluir essa aplicação: já existem tentativas de aluno vinculadas a ela. Encerre-a em vez de excluir.";

        // Checa dependentes ANTES de tentar excluir, em vez de descobrir só ao capturar a
        // exceção do EF (a versão antiga comparava ex.Message.Contains("severed"), frágil a
        // mudança de texto/versão do EF Core). Isso cobre também o caso em que as
        // RespostaProvaOnline já estavam rastreadas neste DbContext (ex.: usuário visitou a
        // tela de resultados antes de excluir) — se estão rastreadas é porque já existem no
        // banco, então essa query já as encontra.
        var temTentativas = await db.RespostasProvaOnline.AnyAsync(r => r.AplicacaoProvaId == aplicacao.Id);
        if (temTentativas)
        {
            throw new OperacaoInvalidaException(mensagemBloqueio);
        }

        try
        {
            db.AplicacoesProva.Remove(aplicacao);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Rede de segurança pra corrida (tentativa criada entre a checagem acima e o
            // SaveChangesAsync) — cenário raro, mas evita vazar uma DbUpdateException crua.
            throw new OperacaoInvalidaException(mensagemBloqueio);
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
