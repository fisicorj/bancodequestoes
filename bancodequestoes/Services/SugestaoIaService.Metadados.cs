using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Sugestão de metadados (Bloom/Tags/Assunto/Item de Alinhamento Curricular) a partir do
// enunciado — parte de SugestaoIaService, ver SugestaoIaService.cs pro porquê da divisão.
public partial class SugestaoIaService
{
    // assuntosDisponiveis/itensMatrizDisponiveis: vocabulário fechado pra IA
    // só reconhecer, nunca inventar (o segundo é opcional, pro Alinhamento Curricular/ENADE).
    public async Task<SugestaoMetadadosDto?> SugerirAsync(
        string enunciado,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis,
        IReadOnlyList<(int ItemMatrizId, string Rotulo)>? itensMatrizDisponiveis = null,
        CancellationToken cancellationToken = default)
    {
        var config = await configuracaoIa.ObterAsync();
        if (!config.Habilitada || string.IsNullOrWhiteSpace(enunciado))
        {
            return null;
        }

        itensMatrizDisponiveis ??= Array.Empty<(int, string)>();

        var prompt = ConstruirPrompt(enunciado, assuntosDisponiveis, itensMatrizDisponiveis);
        var textoJson = await ollama.ChamarAsync(config, config.ModeloEfetivo, config.TimeoutSegundos, PromptSistema, prompt, "sugestão de metadados", cancellationToken);
        if (textoJson is null)
        {
            return null;
        }

        SugestaoBrutaDto? bruto;
        try
        {
            bruto = JsonSerializer.Deserialize<SugestaoBrutaDto>(textoJson, OllamaClient.JsonOpcoes);
        }
        catch (JsonException)
        {
            return null;
        }

        return bruto is null ? null : MapearSugestao(bruto, assuntosDisponiveis, itensMatrizDisponiveis);
    }

    // Atalho do fluxo de classificação em lote — só o Bloom, sem gastar
    // prompt com Tags/Assunto.
    public async Task<NivelBloom?> SugerirBloomAsync(string enunciado, CancellationToken cancellationToken = default)
    {
        var sugestao = await SugerirAsync(enunciado, Array.Empty<(int, string)>(), cancellationToken: cancellationToken);
        return sugestao?.Bloom;
    }

    // Atalho especializado pro Alinhamento Curricular/ENADE — separado porque o
    // catálogo ENADE pode ser grande e só vale a pena pesar o prompt quando o professor pede.
    public async Task<SugestaoMetadadosDto?> SugerirAlinhamentoCurricularAsync(
        string enunciado,
        IReadOnlyList<(int ItemMatrizId, string Rotulo)> itensMatrizDisponiveis,
        CancellationToken cancellationToken = default) =>
        await SugerirAsync(enunciado, Array.Empty<(int, string)>(), itensMatrizDisponiveis, cancellationToken);

    private static SugestaoMetadadosDto MapearSugestao(
        SugestaoBrutaDto bruto,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis,
        IReadOnlyList<(int ItemMatrizId, string Rotulo)> itensMatrizDisponiveis)
    {
        var resultado = new SugestaoMetadadosDto
        {
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

        if (!string.IsNullOrWhiteSpace(bruto.Dificuldade) && Enum.TryParse<Dificuldade>(bruto.Dificuldade.Trim(), ignoreCase: true, out var dificuldade))
        {
            resultado.Dificuldade = dificuldade;
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

        if (!string.IsNullOrWhiteSpace(bruto.ItemMatriz))
        {
            resultado.ItemMatrizRotulo = bruto.ItemMatriz.Trim();
            var matchItem = itensMatrizDisponiveis.FirstOrDefault(i => string.Equals(i.Rotulo, bruto.ItemMatriz.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchItem.ItemMatrizId != 0)
            {
                resultado.ItemMatrizId = matchItem.ItemMatrizId;
            }
        }

        return resultado;
    }

    private const string PromptSistema =
        "Você ajuda um professor brasileiro a classificar questões de prova. " +
        "Responda SOMENTE com um objeto JSON válido, sem texto adicional, no formato: " +
        "{\"bloom\": \"Lembrar|Entender|Aplicar|Analisar|Avaliar|Criar\", " +
        "\"dificuldade\": \"Facil|Media|Dificil\", " +
        "\"tags\": [\"tag curta 1\", \"tag curta 2\"], " +
        "\"assunto\": \"rótulo exato de uma das opções fornecidas, ou vazio se nenhuma se encaixar bem\", " +
        "\"item_matriz\": \"rótulo exato de um dos itens de Alinhamento Curricular/ENADE fornecidos, ou vazio se nenhum se encaixar bem\"}. " +
        "Para \"dificuldade\", avalie a questão como um todo (não só o Bloom) — considere a complexidade do " +
        "enunciado, quantos passos de raciocínio ela exige, se as alternativas (quando houver) são parecidas " +
        "o bastante pra confundir, e o nível de conhecimento prévio necessário: \"Facil\" pra recordação ou " +
        "aplicação direta e óbvia, \"Media\" pra exigir alguma interpretação ou combinar mais de um conceito, " +
        "\"Dificil\" pra exigir raciocínio elaborado, múltiplos passos, ou domínio aprofundado do tema. " +
        "Use no máximo 5 tags curtas em português. Os campos assunto e item_matriz DEVEM ser copiados " +
        "literalmente de uma das opções fornecidas em cada lista (ou ficar vazios) — nunca invente um rótulo novo.";

    private static string ConstruirPrompt(
        string enunciado,
        IReadOnlyList<(int AssuntoId, string Rotulo)> assuntosDisponiveis,
        IReadOnlyList<(int ItemMatrizId, string Rotulo)> itensMatrizDisponiveis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Enunciado da questão:");
        sb.AppendLine(enunciado);

        if (assuntosDisponiveis.Count > 0)
        {
            // Teto defensivo: em CPU o gargalo é prefill, não geração — sem
            // limite, a lista de Assuntos cresce com o banco e estoura o timeout.
            const int limiteAssuntos = 80;
            sb.AppendLine();
            sb.AppendLine("Assuntos disponíveis (escolha um exatamente como escrito, ou deixe vazio):");
            foreach (var (_, rotulo) in assuntosDisponiveis.Take(limiteAssuntos))
            {
                sb.AppendLine($"- {rotulo}");
            }
        }

        if (itensMatrizDisponiveis.Count > 0)
        {
            // Mesmo teto defensivo do catálogo de Assuntos acima, pro
            // catálogo ENADE (pode crescer bastante com várias Áreas de Curso).
            const int limiteItens = 80;
            sb.AppendLine();
            sb.AppendLine("Itens de Alinhamento Curricular/ENADE disponíveis (escolha um exatamente como escrito, ou deixe vazio):");
            foreach (var (_, rotulo) in itensMatrizDisponiveis.Take(limiteItens))
            {
                sb.AppendLine($"- {rotulo}");
            }
        }

        return sb.ToString();
    }

    private class SugestaoBrutaDto
    {
        public string? Bloom { get; set; }

        public string? Dificuldade { get; set; }

        public List<string>? Tags { get; set; }

        public string? Assunto { get; set; }

        [JsonPropertyName("item_matriz")]
        public string? ItemMatriz { get; set; }
    }
}
