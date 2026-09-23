using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Sugestão de metadados e geração de questão via IA (Ollama local ou nuvem), opcional
// e sob demanda; qualquer falha devolve null em vez de propagar exceção, nunca grava sozinho.
// O transporte HTTP com o Ollama (e o log de falhas) mora em OllamaClient (ollama).
//
// Dividido em partial class por feature (auditoria de 09/09/2026 — esse arquivo já tinha
// passado de 600+ linhas, um método por funcionalidade de IA, sem parar de crescer a cada
// feature nova): este arquivo só guarda o que é comum a todas — construtor,
// HabilitadaAsync/ModeloVisaoConfiguradoAsync/TestarConexaoAsync. Cada funcionalidade
// (prompt + DTO bruto + método público) mora no seu próprio arquivo:
//   SugestaoIaService.Metadados.cs         — SugerirAsync/SugerirBloomAsync/SugerirAlinhamentoCurricularAsync
//   SugestaoIaService.RevisaoExtracao.cs   — SugerirRevisaoExtracaoAsync
//   SugestaoIaService.Retranscricao.cs     — SugerirRetranscricaoAsync
//   SugestaoIaService.Geracao.cs           — GerarQuestaoMultiplaEscolhaAsync
//   SugestaoIaService.Explicacao.cs        — SugerirExplicacaoAsync
//   SugestaoIaService.RespostaDiscursiva.cs — SugerirRespostaEsperadaDiscursivaAsync
// http/ollama/configuracaoIa (parâmetros do construtor primário) ficam disponíveis nos
// outros arquivos da mesma partial class normalmente — não precisa repetir o construtor.
public partial class SugestaoIaService(HttpClient http, OllamaClient ollama, ConfiguracaoIaService configuracaoIa)
{
    // Lê a config do banco a cada chamada (quase de graça) em vez de
    // cachear — mudar em /admin/configuracao-ia tem efeito imediato.
    public async Task<bool> HabilitadaAsync() => (await configuracaoIa.ObterAsync()).Habilitada;

    // Usado pela UI pra decidir entre mostrar o botão de retranscrição visual
    // funcional ou uma dica linkando pra /admin/configuracao-ia — nunca tenta
    // a chamada só pra descobrir que o modelo de visão não foi configurado.
    public async Task<bool> ModeloVisaoConfiguradoAsync()
    {
        var config = await configuracaoIa.ObterAsync();
        return config.Habilitada && !string.IsNullOrWhiteSpace(config.ModeloVisaoEfetivo);
    }

    // Chamada mínima ("oi") pro(s) botão(ões) "Testar conexão" — timeout próprio,
    // mais curto, já que não envolve catálogo nenhum. modelo: null testa o modelo
    // de texto (ModeloEfetivo); passe ModeloVisaoEfetivo pra testar o de visão —
    // são modelos diferentes, então cada um precisa do seu próprio teste (uma
    // conexão OK com o modelo de texto não garante nada sobre o de visão, e vice-versa).
    public async Task<(bool Sucesso, string Mensagem)> TestarConexaoAsync(string? modelo = null, CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        var modeloEfetivo = modelo ?? config.ModeloEfetivo;
        if (string.IsNullOrWhiteSpace(modeloEfetivo))
        {
            return (false, "Nenhum modelo configurado pra testar.");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var corpo = new
            {
                model = modeloEfetivo,
                messages = new[] { new { role = "user", content = "oi" } },
                stream = false,
                think = false,
            };

            using var conteudo = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrlEfetiva.TrimEnd('/')}/api/chat")
            {
                Content = conteudo,
            };
            if (config.Modo == ModoExecucaoIa.Nuvem && !string.IsNullOrWhiteSpace(config.ApiKeyNuvem))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKeyNuvem);
            }

            using var resposta = await http.SendAsync(request, linkedCts.Token);
            if (!resposta.IsSuccessStatusCode)
            {
                var corpoErro = await resposta.Content.ReadAsStringAsync(linkedCts.Token);
                return (false, $"{config.Modo} respondeu {(int)resposta.StatusCode} {resposta.StatusCode}: {corpoErro}".Trim());
            }

            return (true, $"Conexão OK — {config.Modo} respondeu com o modelo \"{modeloEfetivo}\".");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var motivo = ex is TaskCanceledException ? "timeout de 30s (modelo ainda carregando na memória?)" : ex.Message;
            return (false, $"Não foi possível conectar ({motivo}).");
        }
    }
}
