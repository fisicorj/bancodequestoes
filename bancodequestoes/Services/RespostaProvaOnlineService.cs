using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Fluxo do aluno (iniciar/salvar/enviar) + correção automática + liberação da nota pelo
// professor. AplicacaoProvaService já garante que a Prova só tem tipos auto-corrigíveis.
public class RespostaProvaOnlineService(ApplicationDbContext db)
{
    // --- Fluxo do aluno ---

    // Só uma tentativa por Aluno/Aplicação (regra de negócio confirmada com o professor):
    // uma tentativa Enviada/Liberada já existente bloqueia uma nova, mas uma EmAndamento é
    // retomada em vez de recriada (o aluno pode ter recarregado a página, por exemplo).
    public async Task<RespostaProvaOnline> IniciarOuRetomarAsync(int aplicacaoId, int alunoId)
    {
        var aplicacao = await db.AplicacoesProva
            .Include(a => a.Prova)
                .ThenInclude(p => p!.ProvaQuestoes)
            .FirstOrDefaultAsync(a => a.Id == aplicacaoId)
            ?? throw new OperacaoInvalidaException("Código de acesso inválido.");

        if (aplicacao.Status != StatusAplicacaoProva.Aberta)
        {
            throw new OperacaoInvalidaException("Essa aplicação está encerrada.");
        }

        if (aplicacao.DataLimite is { } prazo && DateTime.UtcNow > prazo)
        {
            throw new OperacaoInvalidaException("O prazo para responder essa prova já passou.");
        }

        var matriculado = await db.TurmasAlunos
            .AnyAsync(ta => ta.TurmaId == aplicacao.TurmaId && ta.AlunoId == alunoId && ta.Ativa);
        if (!matriculado)
        {
            throw new OperacaoInvalidaException("Você não está matriculado na turma dessa aplicação.");
        }

        var existente = await db.RespostasProvaOnline
            .Include(r => r.Respostas)
            .FirstOrDefaultAsync(r => r.AplicacaoProvaId == aplicacaoId && r.AlunoId == alunoId);

        if (existente is not null)
        {
            if (existente.Status != StatusRespostaProvaOnline.EmAndamento)
            {
                throw new OperacaoInvalidaException("Você já enviou essa prova — não é permitido responder novamente.");
            }

            return existente;
        }

        var tentativa = new RespostaProvaOnline
        {
            AplicacaoProvaId = aplicacaoId,
            AlunoId = alunoId,
            Respostas = aplicacao.Prova!.ProvaQuestoes
                .Select(pq => new RespostaQuestaoOnline { QuestaoId = pq.QuestaoId })
                .ToList(),
        };

        db.RespostasProvaOnline.Add(tentativa);
        await db.SaveChangesAsync();
        return tentativa;
    }

    // Carrega tudo que a tela de resposta precisa: questões (com Ordem/Valor via
    // ProvaQuestao), alternativas de múltipla escolha, e as respostas já salvas.
    public Task<RespostaProvaOnline?> ObterParaResponderAsync(int id) =>
        db.RespostasProvaOnline
            .Include(r => r.AplicacaoProva)
                .ThenInclude(a => a!.Prova)
                    .ThenInclude(p => p!.ProvaQuestoes)
                        .ThenInclude(pq => pq.Questao)
            .Include(r => r.Respostas)
                .ThenInclude(rq => rq.RespostasLacunas)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task SalvarMultiplaEscolhaAsync(int respostaQuestaoOnlineId, char? letra)
    {
        var resposta = await CarregarParaEdicaoAsync(respostaQuestaoOnlineId);
        resposta.RespostaMultiplaEscolha = letra is char c ? char.ToUpperInvariant(c) : null;
        await db.SaveChangesAsync();
    }

    public async Task SalvarCertoErradoAsync(int respostaQuestaoOnlineId, bool? valor)
    {
        var resposta = await CarregarParaEdicaoAsync(respostaQuestaoOnlineId);
        resposta.RespostaCertoErrado = valor;
        await db.SaveChangesAsync();
    }

    public async Task SalvarNumericaAsync(int respostaQuestaoOnlineId, decimal? valor)
    {
        var resposta = await CarregarParaEdicaoAsync(respostaQuestaoOnlineId);
        resposta.RespostaNumerica = valor;
        await db.SaveChangesAsync();
    }

    // Substitui todas as lacunas de uma vez (a tela manda o conjunto inteiro a cada
    // autosave) — mais simples que sincronizar item a item.
    public async Task SalvarLacunasAsync(int respostaQuestaoOnlineId, Dictionary<int, string> respostasPorOrdem)
    {
        var resposta = await CarregarParaEdicaoAsync(respostaQuestaoOnlineId, incluirLacunas: true);

        db.RespostasLacunaOnline.RemoveRange(resposta.RespostasLacunas);
        resposta.RespostasLacunas = respostasPorOrdem
            .Select(kv => new RespostaLacunaOnline { RespostaQuestaoOnlineId = resposta.Id, Ordem = kv.Key, RespostaTexto = kv.Value })
            .ToList();

        await db.SaveChangesAsync();
    }

    private async Task<RespostaQuestaoOnline> CarregarParaEdicaoAsync(int respostaQuestaoOnlineId, bool incluirLacunas = false)
    {
        var query = db.RespostasQuestaoOnline.Include(r => r.RespostaProvaOnline).AsQueryable();
        if (incluirLacunas)
        {
            query = query.Include(r => r.RespostasLacunas);
        }

        var resposta = await query.FirstOrDefaultAsync(r => r.Id == respostaQuestaoOnlineId)
            ?? throw new OperacaoInvalidaException("Resposta não encontrada.");

        if (resposta.RespostaProvaOnline!.Status != StatusRespostaProvaOnline.EmAndamento)
        {
            throw new OperacaoInvalidaException("Essa tentativa já foi enviada — não é mais possível alterar respostas.");
        }

        return resposta;
    }

    // Enviar é o mesmo caminho pro botão "Enviar" do aluno (motivo padrão) e pro
    // encerramento forçado do anti-cola (motivo PerdaDeFoco/SaidaDeTelaCheia) — a diferença é
    // só QUEM chama e com qual motivo, a correção é idêntica nos dois casos.
    public async Task<RespostaProvaOnline> EnviarAsync(int respostaProvaOnlineId, MotivoEncerramento motivo = MotivoEncerramento.EnviadoPeloAluno)
    {
        var tentativa = await db.RespostasProvaOnline
            .Include(r => r.Respostas)
                .ThenInclude(rq => rq.RespostasLacunas)
            .Include(r => r.AplicacaoProva)
            .FirstOrDefaultAsync(r => r.Id == respostaProvaOnlineId)
            ?? throw new OperacaoInvalidaException("Tentativa não encontrada.");

        if (tentativa.Status != StatusRespostaProvaOnline.EmAndamento)
        {
            throw new OperacaoInvalidaException("Essa tentativa já foi enviada.");
        }

        var valoresPorQuestao = await db.ProvasQuestoes
            .Where(pq => pq.ProvaId == tentativa.AplicacaoProva!.ProvaId)
            .ToDictionaryAsync(pq => pq.QuestaoId, pq => pq.Valor);

        decimal notaTotal = 0;
        foreach (var resposta in tentativa.Respostas)
        {
            // Sem Valor configurado na prova (ProvaQuestao.Valor nulo), cada questão vale 1
            // ponto por padrão — só pra sempre existir uma nota numérica, mesmo em provas que
            // nunca usaram o sistema de pontuação por questão.
            var peso = valoresPorQuestao.GetValueOrDefault(resposta.QuestaoId) ?? 1m;
            var correta = await CorrigirQuestaoAsync(resposta);

            resposta.Correta = correta;
            resposta.PontuacaoObtida = correta ? peso : 0;
            notaTotal += resposta.PontuacaoObtida.Value;
        }

        tentativa.NotaTotal = notaTotal;
        tentativa.Status = StatusRespostaProvaOnline.Enviada;
        tentativa.FinalizadoEm = DateTime.UtcNow;
        tentativa.MotivoEncerramento = motivo;

        await db.SaveChangesAsync();
        return tentativa;
    }

    // Compara a resposta do aluno contra o gabarito de acordo com o TipoQuestao — só os 4
    // tipos de TiposAutoCorrigiveis chegam aqui (garantido na criação da AplicacaoProva).
    private async Task<bool> CorrigirQuestaoAsync(RespostaQuestaoOnline resposta)
    {
        var tipo = await db.Questoes
            .Where(q => q.Id == resposta.QuestaoId)
            .Select(q => q.TipoQuestao)
            .FirstOrDefaultAsync();

        switch (tipo)
        {
            case TipoQuestao.MultiplaEscolha:
            {
                var questao = await db.Set<QuestaoMultiplaEscolha>().FirstAsync(q => q.Id == resposta.QuestaoId);
                return resposta.RespostaMultiplaEscolha is { } letra
                    && char.ToUpperInvariant(letra) == char.ToUpperInvariant(questao.RespostaCorreta);
            }

            case TipoQuestao.CertoErrado:
            {
                var questao = await db.Set<QuestaoCertoErrado>().FirstAsync(q => q.Id == resposta.QuestaoId);
                return resposta.RespostaCertoErrado == questao.RespostaCorreta;
            }

            case TipoQuestao.Numerica:
            {
                var questao = await db.Set<QuestaoNumerica>().FirstAsync(q => q.Id == resposta.QuestaoId);
                return resposta.RespostaNumerica is { } valor
                    && Math.Abs(valor - questao.RespostaEsperada) <= questao.Tolerancia;
            }

            case TipoQuestao.Lacunas:
            {
                var questao = await db.Set<QuestaoLacunas>().Include(q => q.Lacunas).FirstAsync(q => q.Id == resposta.QuestaoId);
                if (questao.Lacunas.Count == 0)
                {
                    return false;
                }

                // Tudo ou nada: todas as lacunas certas pra pontuar a questão inteira (padrão
                // combinado com o professor na falta de uma regra de pontuação parcial).
                var todasCertas = true;
                foreach (var lacuna in questao.Lacunas)
                {
                    var respostaAluno = resposta.RespostasLacunas.FirstOrDefault(r => r.Ordem == lacuna.Ordem);
                    var certa = respostaAluno is not null
                        && string.Equals(respostaAluno.RespostaTexto.Trim(), lacuna.RespostaEsperada.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (respostaAluno is not null)
                    {
                        respostaAluno.Correta = certa;
                    }

                    todasCertas &= certa;
                }

                return todasCertas;
            }

            default:
                // Não deveria acontecer — AplicacaoProvaService bloqueia tipos fora da lista.
                return false;
        }
    }

    // --- Correção manual do professor ---

    public Task<List<RespostaProvaOnline>> ListarPorAplicacaoAsync(int aplicacaoId) =>
        db.RespostasProvaOnline
            .Include(r => r.Aluno)
            .Where(r => r.AplicacaoProvaId == aplicacaoId)
            .OrderBy(r => r.Aluno!.Nome)
            .ToListAsync();

    public Task<RespostaProvaOnline?> ObterDetalheAsync(int id) =>
        db.RespostasProvaOnline
            .Include(r => r.Aluno)
            .Include(r => r.AplicacaoProva)
                .ThenInclude(a => a!.Prova)
            .Include(r => r.Respostas)
                .ThenInclude(rq => rq.Questao)
            .Include(r => r.Respostas)
                .ThenInclude(rq => rq.RespostasLacunas)
            .FirstOrDefaultAsync(r => r.Id == id);

    // Liberar é o que faz a nota aparecer pro aluno — nunca automático, sempre uma ação
    // deliberada do professor depois de revisar (regra de negócio confirmada).
    public async Task LiberarAsync(RespostaProvaOnline tentativa)
    {
        if (tentativa.Status != StatusRespostaProvaOnline.Enviada)
        {
            throw new OperacaoInvalidaException("Só é possível liberar tentativas que já foram enviadas e ainda não foram liberadas.");
        }

        tentativa.Status = StatusRespostaProvaOnline.Liberada;
        tentativa.LiberadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // --- Exibição pro aluno (tela pública /responder) ---

    // Só os dados necessários pra RENDERIZAR a pergunta — nunca o gabarito
    // (RespostaCorreta/RespostaEsperada/Tolerancia ficam de fora de propósito, mesmo essa
    // tela sendo pública e sem login).
    public async Task<List<QuestaoOnlineExibicao>> ObterQuestoesParaExibicaoAsync(int respostaProvaOnlineId)
    {
        var tentativa = await db.RespostasProvaOnline
            .Include(r => r.AplicacaoProva)
                .ThenInclude(a => a!.Prova)
                    .ThenInclude(p => p!.ProvaQuestoes)
                        .ThenInclude(pq => pq.Questao)
            .FirstOrDefaultAsync(r => r.Id == respostaProvaOnlineId)
            ?? throw new OperacaoInvalidaException("Tentativa não encontrada.");

        var provaQuestoes = tentativa.AplicacaoProva!.Prova!.ProvaQuestoes.OrderBy(pq => pq.Ordem).ToList();
        var resultado = new List<QuestaoOnlineExibicao>();

        foreach (var pq in provaQuestoes)
        {
            var exibicao = new QuestaoOnlineExibicao
            {
                QuestaoId = pq.QuestaoId,
                Ordem = pq.Ordem,
                Enunciado = pq.Questao!.Enunciado,
                Tipo = pq.Questao.TipoQuestao,
                Valor = pq.Valor,
            };

            switch (pq.Questao.TipoQuestao)
            {
                case TipoQuestao.MultiplaEscolha:
                    exibicao.Alternativas = await db.Set<QuestaoMultiplaEscolha>()
                        .Where(q => q.Id == pq.QuestaoId)
                        .SelectMany(q => q.Alternativas)
                        .OrderBy(a => a.Letra)
                        .Select(a => new AlternativaExibicao { Letra = a.Letra, Texto = a.Texto })
                        .ToListAsync();
                    break;

                case TipoQuestao.Lacunas:
                    exibicao.QuantidadeLacunas = await db.Set<QuestaoLacunas>()
                        .Where(q => q.Id == pq.QuestaoId)
                        .SelectMany(q => q.Lacunas)
                        .CountAsync();
                    break;
            }

            resultado.Add(exibicao);
        }

        return resultado;
    }
}

public sealed class QuestaoOnlineExibicao
{
    public int QuestaoId { get; set; }
    public int Ordem { get; set; }
    public required string Enunciado { get; set; }
    public TipoQuestao Tipo { get; set; }
    public decimal? Valor { get; set; }

    public List<AlternativaExibicao> Alternativas { get; set; } = new();
    public int QuantidadeLacunas { get; set; }
}

public sealed class AlternativaExibicao
{
    public char Letra { get; set; }
    public required string Texto { get; set; }
}
