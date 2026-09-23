using System.Text.Json;
using System.Text.Json.Serialization;

namespace BancoQuestoes.Services;

// Sugestão de resposta esperada (padrão de correção) de questão Discursiva — parte de
// SugestaoIaService, ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // Sugere a resposta esperada (padrão de correção) de uma questão Discursiva
    // a partir só do enunciado — diferente de SugerirExplicacaoAsync (que explica
    // uma resposta JÁ definida), aqui a IA propõe o que ainda não existe; por
    // isso a revisão do professor é ainda mais importante (nunca aplicado sozinho).
    public async Task<string?> SugerirRespostaEsperadaDiscursivaAsync(
        string enunciado,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || string.IsNullOrWhiteSpace(enunciado))
        {
            return null;
        }

        var prompt = $"Enunciado da questão discursiva:\n{enunciado}";
        var textoJson = await ollama.ChamarAsync(config, config.ModeloEfetivo, config.TimeoutSegundos, PromptSistemaRespostaDiscursiva, prompt, "resposta esperada de discursiva", cancellationToken);
        if (textoJson is null)
        {
            return null;
        }

        RespostaDiscursivaBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<RespostaDiscursivaBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        var resposta = bruto?.RespostaEsperada?.Trim();
        return string.IsNullOrWhiteSpace(resposta) ? null : resposta;
    }

    private const string PromptSistemaRespostaDiscursiva =
        "Você propõe a resposta esperada (padrão de correção/gabarito) de uma questão discursiva de prova " +
        "universitária brasileira, a partir só do enunciado — o professor ainda não escreveu nenhuma resposta, " +
        "então esta é só uma SUGESTÃO de ponto de partida pra ele revisar, corrigir e completar, nunca a " +
        "palavra final. Responda SOMENTE com um objeto JSON válido, sem texto adicional, no formato: " +
        "{\"resposta_esperada\": \"texto da resposta esperada\"}. Escreva em português, de forma objetiva e " +
        "completa o suficiente pra servir de gabarito (os pontos-chave que uma boa resposta deveria cobrir), " +
        "sem ser um texto corrido longo demais. Pode usar Markdown simples (negrito, itálico, listas) e LaTeX " +
        "entre $...$ se o enunciado envolver fórmulas.";

    private class RespostaDiscursivaBrutaDto
    {
        [JsonPropertyName("resposta_esperada")]
        public string? RespostaEsperada { get; set; }
    }
}
