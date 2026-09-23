using System.Text;
using System.Text.Json;

namespace BancoQuestoes.Services;

// Revisão de possíveis problemas de extração do PDF (Importador ENADE) — parte de
// SugestaoIaService, ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // Sinaliza possíveis problemas de EXTRAÇÃO do PDF (Importador ENADE) — nunca
    // corrige nada sozinha, só aponta o que parece suspeito (texto truncado,
    // alternativa incompleta, referência a figura que não veio junto etc.)
    // pro professor decidir o que revisar primeiro. Igual às outras chamadas:
    // opcional, sob demanda, null em qualquer falha.
    public async Task<List<string>?> SugerirRevisaoExtracaoAsync(
        string enunciado,
        IReadOnlyList<string> alternativas,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || string.IsNullOrWhiteSpace(enunciado))
        {
            return null;
        }

        var prompt = ConstruirPromptRevisaoExtracao(enunciado, alternativas);
        var textoJson = await ollama.ChamarAsync(config, config.ModeloEfetivo, config.TimeoutSegundos, PromptSistemaRevisaoExtracao, prompt, "revisão de extração", cancellationToken);
        if (textoJson is null)
        {
            return null;
        }

        RevisaoExtracaoBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<RevisaoExtracaoBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        return (bruto?.Problemas ?? new List<string>())
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Take(6)
            .ToList();
    }

    private const string PromptSistemaRevisaoExtracao =
        "Você revisa o texto de uma questão de prova extraído automaticamente de um PDF (parser de texto/posição, sem IA e sem OCR de imagem). " +
        "Sua única tarefa é apontar SINAIS de que a extração pode ter dado errado — nunca corrigir, completar ou reescrever o texto. " +
        "Responda SOMENTE com um objeto JSON válido, sem texto adicional, no formato: " +
        "{\"problemas\": [\"descrição curta do problema 1\", \"descrição curta do problema 2\"]}. " +
        "Sinais válidos: frase que termina de forma abrupta/incompleta, palavras coladas ou com caracteres quebrados, " +
        "referência a uma figura/tabela/fórmula que não aparece no texto (pode ser uma imagem que não foi extraída), " +
        "alternativa vazia ou visivelmente cortada, numeração de alternativas fora de ordem ou faltando. " +
        "Se o texto parecer íntegro, responda \"problemas\": []. Nunca invente um problema que não existe " +
        "e nunca proponha o texto corrigido — só descreva o que parece suspeito, em português, no máximo 6 itens.";

    private static string ConstruirPromptRevisaoExtracao(string enunciado, IReadOnlyList<string> alternativas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Enunciado extraído:");
        sb.AppendLine(enunciado);

        if (alternativas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Alternativas extraídas:");
            foreach (var alt in alternativas)
            {
                sb.AppendLine($"- {alt}");
            }
        }

        return sb.ToString();
    }

    private class RevisaoExtracaoBrutaDto
    {
        public List<string>? Problemas { get; set; }
    }
}
