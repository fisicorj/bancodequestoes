using System.Text.Json;

namespace BancoQuestoes.Services;

// Retranscrição visual de página do PDF (Importador ENADE) — parte de SugestaoIaService,
// ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // Retranscreve enunciado/alternativas a partir da IMAGEM da página (Importador
    // ENADE) — só quando o texto extraído do PDF parece corrompido/incompleto.
    // Exige um modelo com suporte a visão (llava, qwen2.5vl, gemma3...); modelo
    // só-texto falha e devolve null aqui, igual a qualquer outra falha de IA.
    // Nunca inclui gabarito: a imagem da prova em si não revela a resposta certa.
    public async Task<RetranscricaoDto?> SugerirRetranscricaoAsync(
        byte[] imagemPaginaPng,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || imagemPaginaPng.Length == 0 || string.IsNullOrWhiteSpace(config.ModeloVisaoEfetivo))
        {
            return null;
        }

        var imagemBase64 = Convert.ToBase64String(imagemPaginaPng);
        var textoJson = await ollama.ChamarAsync(
            config, config.ModeloVisaoEfetivo, config.TimeoutVisaoSegundos, PromptSistemaRetranscricao, PromptUsuarioRetranscricao, "retranscrição visual",
            cancellationToken, imagensBase64Usuario: new[] { imagemBase64 });
        if (textoJson is null)
        {
            return null;
        }

        RetranscricaoBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<RetranscricaoBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        if (bruto is null || string.IsNullOrWhiteSpace(bruto.Enunciado))
        {
            return null;
        }

        return new RetranscricaoDto
        {
            Enunciado = bruto.Enunciado.Trim(),
            Alternativas = (bruto.Alternativas ?? new List<string>())
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .ToList(),
        };
    }

    // Prompt fixo (sem catálogo/vocabulário) — a imagem inteira vai anexada na
    // mensagem "user" via ChamarAsync, então não precisa de ConstruirPrompt aqui.
    private const string PromptSistemaRetranscricao =
        "Você transcreve LITERALMENTE o texto de uma questão de prova a partir da imagem de uma página de PDF. " +
        "Nunca corrija, complete, resuma ou traduza — copie exatamente o que está escrito, preservando erros de digitação se houver. " +
        "Responda SOMENTE com um objeto JSON válido, sem texto adicional, no formato: " +
        "{\"enunciado\": \"texto completo do enunciado, sem as alternativas\", " +
        "\"alternativas\": [\"texto da alternativa A\", \"texto da alternativa B\", \"...\"]}. " +
        "Nas alternativas, NÃO inclua a letra (A, B, C...) nem o marcador — só o texto de cada uma, na ordem em que aparecem. " +
        "Se a questão for discursiva (sem alternativas de múltipla escolha visíveis), devolva \"alternativas\": []. " +
        "Nunca inclua gabarito, resposta correta ou qualquer indicação de qual alternativa é certa — a imagem da prova não mostra isso, e você não deve adivinhar.";

    private const string PromptUsuarioRetranscricao =
        "Transcreva o enunciado e as alternativas (se houver) da questão nesta imagem.";

    private class RetranscricaoBrutaDto
    {
        public string? Enunciado { get; set; }

        public List<string>? Alternativas { get; set; }
    }
}
