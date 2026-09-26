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

    // Cartões resposta são privados por professor, mesmo critério de Prova/AplicacaoProva
    // (itens 18 e correção de IDOR em AplicacaoProvaService) — sem esse filtro, qualquer
    // professor logado listava/via aplicações de cartão de outro, inclusive notas de alunos
    // de outra turma/instituição.
    public async Task<PaginaResultado<CartaoRespostaAplicacao>> ListarAsync(string? meuId, int pagina, int tamanhoPagina)
    {
        var query = db.CartoesRespostaAplicacao
            .Include(c => c.Prova)
            .Include(c => c.Turma)
            .Where(c => c.CriadoPorId == meuId)
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

    // Checagem de posse (mesmo padrão de ExportacaoService.EhDonoDaProvaAsync/
    // AplicacaoProvaService.EhDonoAsync) — usada pelas páginas de detalhe/conferência e pelos
    // endpoints de download de PDF em Program.cs.
    public async Task<bool> EhDonoAsync(int aplicacaoId, string? meuId)
    {
        var criadorId = await db.CartoesRespostaAplicacao
            .Where(c => c.Id == aplicacaoId)
            .Select(c => c.CriadoPorId)
            .FirstOrDefaultAsync();

        return criadorId is not null && criadorId == meuId;
    }

    // Mesma checagem, mas a partir do Id de um CARTÃO individual (não da aplicação) — usada
    // onde só se tem o cartaoId, como na conferência por aluno e no PDF avulso.
    public async Task<bool> EhDonoDoCartaoAsync(int cartaoId, string? meuId)
    {
        var criadorId = await db.CartoesAlunoAplicacao
            .Where(c => c.Id == cartaoId)
            .Select(c => c.CartaoRespostaAplicacao!.CriadoPorId)
            .FirstOrDefaultAsync();

        return criadorId is not null && criadorId == meuId;
    }

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

        // Achado médio da auditoria (N+1): antes buscava uma questão por vez dentro do loop —
        // como esse método roda uma vez POR FOTO no upload em lote, uma prova de 30 questões
        // com 60 fotos virava ~1.800 queries sequenciais numa única submissão. Uma query só,
        // trazendo todas de uma vez, resolve isso independente de quantas vezes for chamado.
        var idsQuestoes = provaQuestoes.Select(pq => pq.QuestaoId).ToList();
        var questoesPorId = await db.Set<QuestaoMultiplaEscolha>()
            .Include(q => q.Alternativas)
            .Where(q => idsQuestoes.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id);

        foreach (var pq in provaQuestoes)
        {
            var questao = questoesPorId[pq.QuestaoId];

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

        // Achado médio da auditoria (mesmo caso de AplicacaoProvaService.CriarAsync): sem essa
        // checagem, um ProvaId de outro professor passava direto e o cartão resposta gerado
        // continha o gabarito de uma prova alheia.
        var provaEhDoProfessor = await db.Provas.AnyAsync(p => p.Id == modelo.ProvaId && p.CriadoPorId == criadoPorId);
        if (!provaEhDoProfessor)
        {
            throw new OperacaoInvalidaException("Prova não encontrada.");
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

    // Achado baixo da auditoria: os métodos de mutação abaixo confiavam só na checagem de
    // posse feita na camada Razor (acessoNegado no OnInitializedAsync) — um Id passado direto
    // pro Service (chamada futura via API, teste, outro ponto de entrada) não era revalidado
    // aqui dentro. Reforço em profundidade: cada método de mutação agora recebe meuId e
    // confere posse ele mesmo, igual já era feito pra leitura (EhDonoAsync/EhDonoDoCartaoAsync).
    private const string MensagemSemPermissao = "Você não tem permissão para essa operação.";

    // Achado médio da auditoria: aluno matriculado na turma DEPOIS da aplicação já criada não
    // tinha nenhuma forma de receber um cartão (diferente de AplicacaoProvaService, que já
    // tinha GerarAcessoAsync pra esse caso) — idempotente, mesmo padrão do outro Service.
    public async Task<CartaoAlunoAplicacao> GerarCartaoAsync(int aplicacaoId, int alunoId, string? meuId)
    {
        if (!await EhDonoAsync(aplicacaoId, meuId))
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

        var existente = await db.CartoesAlunoAplicacao
            .FirstOrDefaultAsync(c => c.CartaoRespostaAplicacaoId == aplicacaoId && c.AlunoId == alunoId);
        if (existente is not null)
        {
            return existente;
        }

        var cartao = new CartaoAlunoAplicacao
        {
            CartaoRespostaAplicacaoId = aplicacaoId,
            AlunoId = alunoId,
            Token = await GerarTokenUnicoAsync(),
        };
        db.CartoesAlunoAplicacao.Add(cartao);
        await db.SaveChangesAsync();
        return cartao;
    }

    public async Task ExcluirAsync(CartaoRespostaAplicacao aplicacao)
    {
        // CartaoAlunoAplicacao é Cascade (não Restrict) nessa FK, então excluir a aplicação
        // apagaria em cascata, sem aviso, qualquer leitura ou correção já feita. Antes disso
        // era detectado só de forma incidental por um catch de
        // InvalidOperationException.Message.Contains("severed") — frágil a mudança de texto/
        // versão do EF — e nem cobria esse caso real (Cascade não gera essa exceção). Checar o
        // estado de negócio direto é mais robusto e evita perder dado corrigido.
        var temLeitura = await db.CartoesAlunoAplicacao
            .AnyAsync(c => c.CartaoRespostaAplicacaoId == aplicacao.Id && c.Status != StatusCartaoAluno.PendenteEnvio);
        if (temLeitura)
        {
            throw new OperacaoInvalidaException(
                "Não foi possível excluir essa aplicação de cartão resposta: já existem cartões lidos ou confirmados, que seriam perdidos.");
        }

        try
        {
            db.CartoesRespostaAplicacao.Remove(aplicacao);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new OperacaoInvalidaException("Não foi possível excluir essa aplicação de cartão resposta.");
        }
    }

    // --- Upload em lote (Etapa 6): processa uma foto e já grava a leitura ---

    // Lê o QR pra descobrir de qual cartão é a foto, confere que é da aplicação certa (evita
    // um professor enviar sem querer a foto de outra turma/aplicação) e grava a leitura.
    // Lança OperacaoInvalidaException com uma mensagem apresentável em cada caso de falha —
    // a tela de upload mostra isso por foto, sem travar o processamento das demais.
    public async Task<string> ProcessarFotoAsync(int aplicacaoId, byte[] fotoBytes, string? meuId)
    {
        if (!await EhDonoAsync(aplicacaoId, meuId))
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

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

        await RegistrarLeituraAsync(cartao.Id, resultado.RespostasPorQuestao, meuId);
        return cartao.Aluno!.Nome;
    }

    // --- Registro da leitura de uma foto (chamado por ProcessarFotoAsync acima) ---

    // Grava o que a leitura da foto detectou (sempre como Enviado — nunca Confirmado
    // automaticamente) e recalcula a nota provisória; a tela de conferência (Etapa 7) é quem
    // corrige e confirma de fato.
    public async Task RegistrarLeituraAsync(int cartaoId, Dictionary<int, (char? Letra, bool Ambigua)> respostasPorQuestao, string? meuId)
    {
        if (!await EhDonoDoCartaoAsync(cartaoId, meuId))
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

        var cartao = await db.CartoesAlunoAplicacao
            .Include(c => c.Respostas)
            .FirstOrDefaultAsync(c => c.Id == cartaoId)
            ?? throw new OperacaoInvalidaException("Cartão não encontrado.");

        // Um cartão Confirmado já passou pela conferência manual do professor — reenviar a
        // foto (por engano, ou de propósito) não pode apagar silenciosamente uma correção já
        // fechada; é preciso reabrir a conferência de propósito antes de aceitar nova leitura.
        if (cartao.Status == StatusCartaoAluno.Confirmado)
        {
            throw new OperacaoInvalidaException(
                "Esse cartão já foi confirmado — a nova leitura foi ignorada para não sobrescrever a correção. Se precisar refazer, reabra o cartão na tela de conferência antes de reenviar a foto.");
        }

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

    public async Task ConfirmarRespostaAsync(int respostaCartaoQuestaoId, char? letraConfirmada, string? meuId)
    {
        var resposta = await db.RespostasCartaoQuestao
            .Include(r => r.CartaoAlunoAplicacao)
                .ThenInclude(c => c!.CartaoRespostaAplicacao)
            .FirstOrDefaultAsync(r => r.Id == respostaCartaoQuestaoId)
            ?? throw new OperacaoInvalidaException("Resposta não encontrada.");

        if (resposta.CartaoAlunoAplicacao!.CartaoRespostaAplicacao!.CriadoPorId != meuId)
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

        resposta.LetraConfirmada = letraConfirmada is char c ? char.ToUpperInvariant(c) : null;
        resposta.Ambigua = false;
        await db.SaveChangesAsync();
    }

    // Fecha a conferência: todas as respostas precisam ter uma letra confirmada (ou
    // explicitamente em branco) antes de virar Confirmado — impede salvar com pendência.
    public async Task ConfirmarCartaoAsync(int cartaoId, string? meuId)
    {
        if (!await EhDonoDoCartaoAsync(cartaoId, meuId))
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

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

    // Escape hatch pro Fix 3: só assim um cartão Confirmado aceita nova leitura de foto de
    // novo — a reabertura tem que ser um clique deliberado do professor, nunca implícita no
    // simples reenvio de uma foto.
    public async Task ReabrirCartaoAsync(int cartaoId, string? meuId)
    {
        if (!await EhDonoDoCartaoAsync(cartaoId, meuId))
        {
            throw new OperacaoInvalidaException(MensagemSemPermissao);
        }

        var cartao = await db.CartoesAlunoAplicacao
            .FirstOrDefaultAsync(c => c.Id == cartaoId)
            ?? throw new OperacaoInvalidaException("Cartão não encontrado.");

        if (cartao.Status != StatusCartaoAluno.Confirmado)
        {
            throw new OperacaoInvalidaException("Esse cartão ainda não está confirmado.");
        }

        cartao.Status = StatusCartaoAluno.Enviado;
        cartao.CorrigidoEm = null;
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
