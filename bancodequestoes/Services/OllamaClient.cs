using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Miolo HTTP compartilhado com o Ollama (/api/chat) — isolado à parte pra
// SugestaoIaService.cs não crescer sem parar; único ponto do projeto que fala
// com a API do Ollama. Cada funcionalidade (metadados, geração, retranscrição
// visual...) mora em SugestaoIaService e só usa este cliente pro transporte.
public sealed class OllamaClient(HttpClient http, ILogger<OllamaClient> logger)
{
    // modelo/timeoutSegundos: explícitos — nunca lidos de dentro daqui, porque a
    // mesma config pode ter mais de um modelo/timeout (texto vs visão, ver
    // ConfiguracaoIa.ModeloEfetivo/ModeloVisaoEfetivo e TimeoutSegundos/
    // TimeoutVisaoSegundos); quem decide qual usar é o chamador.
    // imagensBase64Usuario: opcional, anexada só na mensagem "user" (Ollama
    // aceita "images": [base64...] em cada mensagem do /api/chat) — usado pela
    // retranscrição visual de página; chamadas só-texto não passam nada aqui.
    public async Task<string?> ChamarAsync(
        ConfiguracaoIa config,
        string modelo,
        int timeoutSegundos,
        string promptSistema,
        string promptUsuario,
        string contexto,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? imagensBase64Usuario = null)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSegundos));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            object mensagemUsuario = imagensBase64Usuario is { Count: > 0 }
                ? new { role = "user", content = promptUsuario, images = imagensBase64Usuario }
                : new { role = "user", content = promptUsuario };

            var corpo = new
            {
                model = modelo,
                messages = new object[]
                {
                    new { role = "system", content = promptSistema },
                    mensagemUsuario,
                },
                format = "json",
                stream = false,
                // think=false desliga o "raciocínio" longo de modelos híbridos (que
                // estouraria timeout em CPU); modelos sem esse recurso ignoram o campo.
                think = false,
            };

            using var conteudo = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrlEfetiva.TrimEnd('/')}/api/chat")
            {
                Content = conteudo,
            };

            // Modo Nuvem exige API key no header Authorization; Local nunca manda esse header.
            if (config.Modo == ModoExecucaoIa.Nuvem && !string.IsNullOrWhiteSpace(config.ApiKeyNuvem))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKeyNuvem);
            }

            using var resposta = await http.SendAsync(request, linkedCts.Token);

            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Ollama ({Modo}) respondeu {Status} ao pedir {Contexto}",
                    config.Modo, resposta.StatusCode, contexto);
                return null;
            }

            var corpoResposta = await resposta.Content.ReadAsStringAsync(linkedCts.Token);
            var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(corpoResposta, JsonOpcoes);
            var textoJson = envelope?.Message?.Content;
            return string.IsNullOrWhiteSpace(textoJson) ? null : textoJson;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Falha de rede, timeout ou JSON inválido nunca estoura erro pro
            // professor, só "sem resultado desta vez" (log distingue timeout dos demais).
            var motivo = ex is TaskCanceledException
                ? $"timeout de {timeoutSegundos}s (modelo ainda carregando/gerando?)"
                : ex.GetType().Name;
            logger.LogWarning(ex, "Falha ao obter {Contexto} via Ollama {Modo} ({Motivo})", contexto, config.Modo, motivo);
            return null;
        }
    }

    // Exposto pra SugestaoIaService reaproveitar as mesmas opções ao desserializar
    // os DTOs específicos de cada funcionalidade (evita duplicar a configuração).
    public static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMensagem? Message { get; set; }
    }

    private class OllamaMensagem
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
