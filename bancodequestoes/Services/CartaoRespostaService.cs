using BancoQuestoes.CartaoResposta;
using BancoQuestoes.Data;
using BancoQuestoes.Exportacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Cria e gerencia aplicações de leitura de cartão resposta (prova IMPRESSA, corrigida por
// foto) — fluxo separado da prova online (AplicacaoProvaService). Só múltipla escolha: é o
// único tipo com layout de bolha ("A B C D E") que dá pra ler de uma foto.
public class CartaoRespostaService(ApplicationDbContext db)
{
    // A-E: cobre a esmagadora maioria das provas de múltipla escolha do sistema; uma
    // questão com mais alternativas que isso não caberia no layout impresso do cartão.
    public const int MaxAlternativas = 5;

    private const string AlfabetoToken = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<PaginaResultado<CartaoRespostaAplicacao>> ListarAsync(int pagina, int tamanhoPagina)
    {
        var query = db.CartoesRespostaAplicacao
            .Include(c => c.Prova)
            .Include(c => c.Turma)
            .AsQueryable();

        var total = await query.CountAsync();
        var itens = await query
            .OrderByDescending(c => c.CriadoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaResultado<CartaoRespostaAplicacao> { Itens = itens, Total = total };
    }

    public Task<CartaoRespostaAplicacao?> ObterAsync(int id) =>
        db.CartoesRespostaAplicacao
            .Include(c => c.Prova)
            .Include(c => c.Turma)
            .FirstOrDefaultAsync(c => c.Id == id);

    public Task<List<CartaoAlunoAplicacao>> ListarCartoesAsync(int aplicacaoId) =>
        db.CartoesAlunoAplicacao
            .Include(c => c.Aluno)
            .Where(c => c.CartaoRespostaAplicacaoId == aplicacaoId)
            .OrderBy(c => c.Aluno!.Nome)
            .ToListAsync();

    // Usado tanto pelo gerador de PDF (Etapa 4) quanto pelo leitor de imagem (Etapa 5) — os
    // dois precisam saber exatamente a mesma ordem/gabarito/quantidade de alternativas.
    public async Task<List<QuestaoCartaoInfo>> ObterQuestoesAsync(int aplicacaoId)
    {
        var aplicacao = await db.CartoesRespostaAplicacao
            .Include(c => c.Prova)
                .ThenInclude(p => p!.ProvaQuestoes)
            .FirstOrDefaultAsync(c => c.Id == aplicacaoId)
            ?? throw new OperacaoInvalidaException("Aplicação de cartão resposta não encontrada.");

        var provaQuestoes = aplicacao.Prova!.ProvaQuestoes.OrderBy(pq => pq.Ordem).ToList();
        var resultado = new List<QuestaoCartaoInfo>();

        foreach (var pq in provaQuestoes)
        {
            var questao = await db.Set<QuestaoMultiplaEscolha>()
                .Include(q => q.Alternativas)
                .FirstAsync(q => q.Id == pq.QuestaoId);

            resultado.Add(new QuestaoCartaoInfo
            {
                QuestaoId = questao.Id,
                Ordem = pq.Ordem,
                Valor = pq.Valor,
                RespostaCorreta = char.ToUpperInvariant(questao.RespostaCorreta),
                QuantidadeAlternativas = questao.Alternativas.Count,
            });
        }

        return resultado;
    }

    public Task<CartaoAlunoAplicacao?> ObterCartaoPorTokenAsync(string token) =>
        db.CartoesAlunoAplicacao
            .Include(c => c.Aluno)
            .Include(c => c.CartaoRespostaAplicacao)
                .ThenInclude(a => a!.Prova)
            .Include(c => c.CartaoRespostaAplicacao)
                .ThenInclude(a => a!.Turma)
            .Include(c => c.Respostas)
            .FirstOrDefaultAsync(c => c.Token == token);

    public Task<CartaoAlunoAplicacao?> ObterCartaoDetalheAsync(int id) =>
        db.CartoesAlunoAplicacao
            .Include(c => c.Aluno)
            .Include(c => c.CartaoRespostaAplicacao)
                .ThenInclude(a => a!.Prova)
            .Include(c => c.CartaoRespostaAplicacao)
                .ThenInclude(a => a!.Turma)
            .Include(c => c.Respostas)
                .ThenInclude(r => r.Questao)
            .FirstOrDefaultAsync(c => c.Id == id);

    // --- Geração do PDF (Etapa 4) ---

    public async Task<(string NomeArquivo, byte[] Bytes)> GerarPdfLoteAsync(int aplicacaoId)
    {
        var aplicacao = await ObterAsync(aplicacaoId) ?? throw new OperacaoInvalidaException("Aplicação de cartão resposta não encontrada.");
        var questoes = await ObterQuestoesAsync(aplicacaoId);
        var cartoes = await ListarCartoesAsync(aplicacaoId);
        var (disciplinaRotulo, tipoRotulo) = await ObterRotulosProvaAsync(aplicacao.ProvaId);

        var bytes = CartaoRespostaPdfExporter.GerarLote(
            disciplinaRotulo,
            tipoRotulo,
            aplicacao.Turma!.Rotulo,
            questoes,
            cartoes.Select(c => (c.Aluno!.Nome, c.Token)).ToList());

        return ($"cartoes-{aplicacao.Turma.Rotulo}", bytes);
    }

    public async Task<(string NomeArquivo, byte[] Bytes)> GerarPdfUnicoAsync(int cartaoId)
    {
        var cartao = await ObterCartaoDetalheAsync(cartaoId) ?? throw new OperacaoInvalidaException("Cartão não encontrado.");
        var questoes = await ObterQuestoesAsync(cartao.CartaoRespostaAplicacaoId);
        var (disciplinaRotulo, tipoRotulo) = await ObterRotulosProvaAsync(cartao.CartaoRespostaAplicacao!.ProvaId);

        var bytes = CartaoRespostaPdfExporter.GerarUnico(
            disciplinaRotulo,
            tipoRotulo,
            cartao.CartaoRespostaAplicacao.Turma!.Rotulo,
            cartao.Aluno!.Nome,
            cartao.Token,
            questoes);

        return ($"cartao-{cartao.Aluno.Nome}", bytes);
    }

    // Reaproveita o mesmo DTO/rótulo usado na exportação normal da prova (EscopoRotulo já
    // resolve Disciplina/Multidisciplinar/Curso) — assim o cabeçalho do cartão resposta fica
    // consistente com o resto do sistema em vez de duplicar essa lógica aqui.
    private async Task<(string DisciplinaRotulo, string? TipoRotulo)> ObterRotulosProvaAsync(int provaId)
    {
        var dto = await ProvaExportLoader.CarregarAsync(db, provaId);
        return (dto?.EscopoRotulo ?? "", dto?.Tipo?.Rotulo());
    }

    public async Task<CartaoRespostaAplicacao> CriarAsync(CartaoRespostaInput modelo, string? criadoPorId)
    {
        if (modelo.ProvaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma prova.");
        }

        if (modelo.TurmaId == 0)
        {
            throw new OperacaoInvalidaException("Selecione uma turma.");
        }

        // Leitura de cartão resposta só entende bolha marcada — nada de Discursiva,
        // Numérica etc. (diferente da prova online, que também corrige outros tipos).
        var tiposNaoSuportados = await db.ProvasQuestoes
            .Where(pq => pq.ProvaId == modelo.ProvaId)
            .Select(pq => pq.Questao!.TipoQuestao)
            .Where(tipo => tipo != TipoQuestao.MultiplaEscolha)
            .Distinct()
            .ToListAsync();

        if (tiposNaoSuportados.Count > 0)
        {
            var rotulos = string.Join(", ", tiposNaoSuportados.Select(t => t.Rotulo()));
            throw new OperacaoInvalidaException(
                $"Essa prova tem questões do tipo {rotulos} — leitura de cartão resposta só funciona com provas 100% múltipla escolha.");
        }

        var quantidadeAlternativas = await db.ProvasQuestoes
            .Where(pq => pq.ProvaId == modelo.ProvaId)
            .SelectMany(pq => db.Set<QuestaoMultiplaEscolha>().Where(q => q.Id == pq.QuestaoId).Select(q => q.Alternativas.Count))
            .ToListAsync();

        if (quantidadeAlternativas.Any(q => q > MaxAlternativas))
        {
            throw new OperacaoInvalidaException(
                $"Essa prova tem uma questão com mais de {MaxAlternativas} alternativas — o cartão resposta impresso só suporta até {MaxAlternativas} (A a {(char)('A' + MaxAlternativas - 1)}).");
        }

        var alunosIds = await db.TurmasAlunos
            .Where(ta => ta.TurmaId == modelo.TurmaId && ta.Ativa)
            .Select(ta => ta.AlunoId)
            .ToListAsync();

        if (alunosIds.Count == 0)
        {
            throw new OperacaoInvalidaException("Essa turma não tem nenhum aluno matriculado ativo — matricule os alunos antes de gerar os cartões.");
        }

        var aplicacao = new CartaoRespostaAplicacao
        {
            ProvaId = modelo.ProvaId,
            TurmaId = modelo.TurmaId,
            CriadoPorId = criadoPorId,
        };
        db.CartoesRespostaAplicacao.Add(aplicacao);
        await db.SaveChangesAsync();

        foreach (var alunoId in alunosIds)
        {
            db.CartoesAlunoAplicacao.Add(new CartaoAlunoAplicacao
            {
                CartaoRespostaAplicacaoId = aplicacao.Id,
                AlunoId = alunoId,
                Token = await GerarTokenUnicoAsync(),
            });
        }
        await db.SaveChangesAsync();

        return aplicacao;
    }

    public async Task ExcluirAsync(CartaoRespostaAplicacao aplicacao)
    {
        try
        {
            db.CartoesRespostaAplicacao.Remove(aplicacao);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException("Não foi possível excluir essa aplicação de cartão resposta.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("severed", StringComparison.OrdinalIgnoreCase))
        {
            // Mesmo caso do DbUpdateException acima, só que detectado pelo EF antes de bater
            // no banco — ver comentário equivalente em AplicacaoProvaService.ExcluirAsync.
            throw new OperacaoInvalidaException("Não foi possível excluir essa aplicação de cartão resposta.");
        }
    }

    // --- Upload em lote (Etapa 6): processa uma foto e já grava a leitura ---

    // Lê o QR pra descobrir de qual cartão é a foto, confere que é da aplicação certa (evita
    // um professor enviar sem querer a foto de outra turma/aplicação) e grava a leitura.
    // Lança OperacaoInvalidaException com uma mensagem apresentável em cada caso de falha —
    // a tela de upload mostra isso por foto, sem travar o processamento das demais.
    public async Task<string> ProcessarFotoAsync(int aplicacaoId, byte[] fotoBytes)
    {
        var questoes = await ObterQuestoesAsync(aplicacaoId);
        var resultado = LeitorCartaoRespostaService.Ler(fotoBytes, questoes);
        if (!resultado.Ok)
        {
            throw new OperacaoInvalidaException(resultado.Erro!);
        }

        var cartao = await ObterCartaoPorTokenAsync(resultado.Token!)
            ?? throw new OperacaoInvalidaException("O QR Code lido não corresponde a nenhum cartão conhecido.");

        if (cartao.CartaoRespostaAplicacaoId != aplicacaoId)
        {
            throw new OperacaoInvalidaException($"Essa foto é de outro cartão (aluno: {cartao.Aluno?.Nome}), de outra aplicação.");
        }

        await RegistrarLeituraAsync(cartao.Id, resultado.RespostasPorQuestao);
        return cartao.Aluno!.Nome;
    }

    // --- Registro da leitura de uma foto (chamado por ProcessarFotoAsync acima) ---

    // Grava o que a leitura da foto detectou (sempre como Enviado — nunca Confirmado
    // automaticamente) e recalcula a nota provisória; a tela de conferência (Etapa 7) é quem
    // corrige e confirma de fato.
    public async Task RegistrarLeituraAsync(int cartaoId, Dictionary<int, (char? Letra, bool Ambigua)> respostasPorQuestao)
    {
        var cartao = await db.CartoesAlunoAplicacao
            .Include(c => c.Respostas)
            .FirstOrDefaultAsync(c => c.Id == cartaoId)
            ?? throw new OperacaoInvalidaException("Cartão não encontrado.");

        var questoes = await ObterQuestoesAsync(cartao.CartaoRespostaAplicacaoId);

        db.RespostasCartaoQuestao.RemoveRange(cartao.Respostas);
        cartao.Respostas = questoes.Select(q =>
        {
            var (letra, ambigua) = respostasPorQuestao.GetValueOrDefault(q.QuestaoId, (null, true));
            return new RespostaCartaoQuestao
            {
                QuestaoId = q.QuestaoId,
                Ordem = q.Ordem,
                LetraDetectada = letra,
                Ambigua = ambigua,
                LetraConfirmada = ambigua ? null : letra,
            };
        }).ToList();

        cartao.Status = StatusCartaoAluno.Enviado;
        AtualizarNota(cartao, questoes);

        await db.SaveChangesAsync();
    }

    // --- Confirmação manual na tela de conferência (Etapa 7) ---

    public async Task ConfirmarRespostaAsync(int respostaCartaoQuestaoId, char? letraConfirmada)
    {
        var resposta = await db.RespostasCartaoQuestao
            .Include(r => r.CartaoAlunoAplicacao)
            .FirstOrDefaultAsync(r => r.Id == respostaCartaoQuestaoId)
            ?? throw new OperacaoInvalidaException("Resposta não encontrada.");

        resposta.LetraConfirmada = letraConfirmada is char c ? char.ToUpperInvariant(c) : null;
        resposta.Ambigua = false;
        await db.SaveChangesAsync();
    }

    // Fecha a conferência: todas as respostas precisam ter uma letra confirmada (ou
    // explicitamente em branco) antes de virar Confirmado — impede salvar com pendência.
    public async Task ConfirmarCartaoAsync(int cartaoId)
    {
        var cartao = await db.CartoesAlunoAplicacao
            .Include(c => c.Respostas)
            .FirstOrDefaultAsync(c => c.Id == cartaoId)
            ?? throw new OperacaoInvalidaException("Cartão não encontrado.");

        if (cartao.Respostas.Any(r => r.Ambigua))
        {
            throw new OperacaoInvalidaException("Ainda há respostas ambíguas nesse cartão — resolva todas antes de confirmar.");
        }

        var questoes = await ObterQuestoesAsync(cartao.CartaoRespostaAplicacaoId);
        AtualizarNota(cartao, questoes);
        cartao.Status = StatusCartaoAluno.Confirmado;
        cartao.CorrigidoEm = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static void AtualizarNota(CartaoAlunoAplicacao cartao, List<QuestaoCartaoInfo> questoes)
    {
        var gabaritoPorQuestao = questoes.ToDictionary(q => q.QuestaoId, q => q);
        decimal notaTotal = 0;

        foreach (var resposta in cartao.Respostas)
        {
            var info = gabaritoPorQuestao.GetValueOrDefault(resposta.QuestaoId);
            var peso = info?.Valor ?? 1m;
            resposta.Correta = !resposta.Ambigua
                && resposta.LetraConfirmada is { } letra
                && info is not null
                && letra == info.RespostaCorreta;

            if (resposta.Correta)
            {
                notaTotal += peso;
            }
        }

        cartao.NotaTotal = notaTotal;
    }

    private async Task<string> GerarTokenUnicoAsync()
    {
        for (var tentativa = 0; tentativa < 20; tentativa++)
        {
            var token = new string(Enumerable.Range(0, 10).Select(_ => AlfabetoToken[Random.Shared.Next(AlfabetoToken.Length)]).ToArray());
            if (!await db.CartoesAlunoAplicacao.AnyAsync(c => c.Token == token))
            {
                return token;
            }
        }

        throw new OperacaoInvalidaException("Não foi possível gerar um token único para o cartão — tente novamente.");
    }
}

// Snapshot do gabarito/estrutura de uma questão pro cartão — usado pelo gerador de PDF
// (layout das bolhas) e pelo leitor de imagem (o que cada bolha marcada significa), sem
// carregar a entidade inteira nem expor RespostaCorreta em telas do aluno (esse DTO só
// circula em código de servidor/professor).
public sealed class QuestaoCartaoInfo
{
    public int QuestaoId { get; set; }
    public int Ordem { get; set; }
    public decimal? Valor { get; set; }
    public char RespostaCorreta { get; set; }
    public int QuantidadeAlternativas { get; set; }
}
