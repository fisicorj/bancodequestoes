using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Geração de questão de múltipla escolha a partir de material/instrução (tela
// QuestaoGerarIa) — parte de SugestaoIaService, ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // Gera questão de Múltipla Escolha a partir de material (slide/texto/PDF) — só
    // esse tipo por ora; Discursiva exigiria inventar padrão de resposta, mais arriscado.
    public async Task<QuestaoGeradaDto?> GerarQuestaoMultiplaEscolhaAsync(
        string material,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || string.IsNullOrWhiteSpace(material))
        {
            return null;
        }

        var prompt = ConstruirPromptGeracao(material, assuntosDisponiveis);
        var textoJson = await ollama.ChamarAsync(config, config.ModeloEfetivo, config.TimeoutSegundos, PromptSistemaGeracao, prompt, "geração de questão", cancellationToken);
        if (textoJson is null)
        {
            return null;
        }

        QuestaoGeradaBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<QuestaoGeradaBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        return bruto is null ? null : MapearQuestaoGerada(bruto, assuntosDisponiveis);
    }

    // Rejeita geração claramente inutilizável (sem enunciado ou menos de 2
    // alternativas) em vez de devolver questão quebrada — melhor gerar de novo.
    private static QuestaoGeradaDto? MapearQuestaoGerada(
        QuestaoGeradaBrutaDto bruto,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis)
    {
        if (string.IsNullOrWhiteSpace(bruto.Enunciado))
        {
            return null;
        }

        var alternativas = (bruto.Alternativas ?? new List<string>())
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .ToList();

        if (alternativas.Count < 2)
        {
            return null;
        }

        var indice = bruto.RespostaCorretaIndex;
        if (indice < 0 || indice >= alternativas.Count)
        {
            indice = 0;
        }

        var resultado = new QuestaoGeradaDto
        {
            Enunciado = bruto.Enunciado.Trim(),
            Alternativas = alternativas,
            RespostaCorretaIndex = indice,
            Tags = (bruto.Tags ?? new List<string>())
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList(),
        };

        if (!string.IsNullOrWhiteSpace(bruto.Bloom) && Enum.TryParse<NivelBloom>(bruto.Bloom.Trim(), ignoreCase: true, out var nivel))
        {
            resultado.Bloom = nivel;
        }

        if (!string.IsNullOrWhiteSpace(bruto.Assunto))
        {
            resultado.AssuntoRotulo = bruto.Assunto.Trim();
            var match = assuntosDisponiveis.FirstOrDefault(a => string.Equals(a.Rotulo, bruto.Assunto.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.AssuntoId != 0)
            {
                resultado.AssuntoId = match.AssuntoId;
            }
        }

        return resultado;
    }

    private const string PromptSistemaGeracao =
        "Você cria questões de múltipla escolha para provas universitárias brasileiras, a partir do texto que " +
        "o professor forneceu abaixo. Esse texto pode ser de dois tipos — decida pelo teor dele: " +
        "(a) um MATERIAL DE REFERÊNCIA (slide, trecho de livro, apostila): gere UMA questão que teste " +
        "compreensão real do conteúdo dele (não peça decoreba nem cópia literal de frases, e nunca invente " +
        "conteúdo que não esteja nele); ou (b) uma INSTRUÇÃO/PEDIDO direto do professor (ex.: \"crie uma " +
        "questão sobre recursão em Python, nível difícil\" ou \"questão sobre a Revolução Francesa pra nível " +
        "introdutório\"): siga a instrução, usando seu próprio conhecimento sobre o assunto pedido. " +
        "Responda SOMENTE com um objeto JSON válido, sem texto adicional, no formato: " +
        "{\"enunciado\": \"texto da pergunta\", " +
        "\"alternativas\": [\"alternativa 1\", \"alternativa 2\", \"alternativa 3\", \"alternativa 4\"], " +
        "\"resposta_correta_index\": 0, " +
        "\"bloom\": \"Lembrar|Entender|Aplicar|Analisar|Avaliar|Criar\", " +
        "\"tags\": [\"tag curta 1\", \"tag curta 2\"], " +
        "\"assunto\": \"rótulo exato de uma das opções fornecidas, ou vazio se nenhuma se encaixar bem\"}. " +
        "Gere EXATAMENTE 4 alternativas plausíveis, em português, claras e sem ambiguidade — só UMA pode " +
        "estar correta. \"resposta_correta_index\" é a posição (0 a 3) da alternativa correta na lista " +
        "\"alternativas\". O campo assunto DEVE ser copiado literalmente de uma das opções fornecidas (ou " +
        "ficar vazio) — nunca invente um rótulo novo.";

    private static string ConstruirPromptGeracao(
        string material,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis)
    {
        // Teto de caracteres no material/instrução bruto — mesmo raciocínio do
        // limite de Assuntos/Itens acima; texto maior é cortado (a UI avisa).
        const int limiteCaracteresMaterial = 6000;
        var materialCortado = material.Length > limiteCaracteresMaterial
            ? material[..limiteCaracteresMaterial]
            : material;

        var sb = new StringBuilder();
        sb.AppendLine("Texto fornecido pelo professor (material de referência OU instrução/pedido — ver system prompt):");
        sb.AppendLine(materialCortado);

        if (assuntosDisponiveis.Count > 0)
        {
            const int limiteAssuntos = 80;
            sb.AppendLine();
            sb.AppendLine("Assuntos disponíveis (escolha um exatamente como escrito, ou deixe vazio):");
            foreach (var (_, rotulo) in assuntosDisponiveis.Take(limiteAssuntos))
            {
                sb.AppendLine($"- {rotulo}");
            }
        }

        return sb.ToString();
    }

    private class QuestaoGeradaBrutaDto
    {
        public string? Enunciado { get; set; }

        public List<string>? Alternativas { get; set; }

        [JsonPropertyName("resposta_correta_index")]
        public int RespostaCorretaIndex { get; set; }

        public string? Bloom { get; set; }

        public List<string>? Tags { get; set; }

        public string? Assunto { get; set; }
    }
}
