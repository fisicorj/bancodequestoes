using System.Text;
using System.Text.Json;

namespace BancoQuestoes.Services;

// Sugestão de explicação/resolução da resposta certa (campo "Explicação da resposta" do
// formulário de Questão) — parte de SugestaoIaService, ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // Sugere o texto de explicação/resolução da resposta certa (campo
    // "Explicação da resposta" do formulário de Questão) — nunca aplicado
    // sozinho, o professor confere e clica em "Usar" pra copiar pro campo.
    // respostaCorretaDescricao é montada pela UI (ExplicacaoEditor) a partir
    // do gabarito já preenchido na tela; sem gabarito não há o que explicar.
    public async Task<string?> SugerirExplicacaoAsync(
        string enunciado,
        string respostaCorretaDescricao,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || string.IsNullOrWhiteSpace(enunciado) || string.IsNullOrWhiteSpace(respostaCorretaDescricao))
        {
            return null;
        }

        var prompt = ConstruirPromptExplicacao(enunciado, respostaCorretaDescricao);
        var textoJson = await ollama.ChamarAsync(config, config.ModeloEfetivo, config.TimeoutSegundos, PromptSistemaExplicacao, prompt, "explicação da resposta", cancellationToken);
        if (textoJson is null)
        {
            return null;
        }

        ExplicacaoBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<ExplicacaoBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        var explicacao = bruto?.Explicacao?.Trim();
        return string.IsNullOrWhiteSpace(explicacao) ? null : explicacao;
    }

    private const string PromptSistemaExplicacao =
        "Você escreve a explicação/resolução comentada da resposta certa de uma questão de prova universitária " +
        "brasileira, pra um professor revisar antes de publicar no gabarito comentado. Responda SOMENTE com um " +
        "objeto JSON válido, sem texto adicional, no formato: {\"explicacao\": \"texto da explicação\"}. " +
        "Explique OBJETIVAMENTE por que a resposta informada é a correta (e, se fizer sentido, por que as " +
        "demais opções estão erradas), em português, em um parágrafo curto ou dois. Pode usar Markdown simples " +
        "(negrito, itálico, listas) e LaTeX entre $...$ se a questão envolver fórmulas. Nunca conteste, corrija " +
        "ou troque a resposta informada como correta — assuma que ela está certa e só explique o raciocínio.";

    private static string ConstruirPromptExplicacao(string enunciado, string respostaCorretaDescricao)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Enunciado da questão:");
        sb.AppendLine(enunciado);
        sb.AppendLine();
        sb.AppendLine("Resposta certa (não conteste, só explique o porquê):");
        sb.AppendLine(respostaCorretaDescricao);
        return sb.ToString();
    }

    private class ExplicacaoBrutaDto
    {
        public string? Explicacao { get; set; }
    }
}
