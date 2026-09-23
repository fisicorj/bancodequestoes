using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Resultado de uma sugestão da IA local — puramente informativo; o professor
// decide, campo a campo, se aceita ou ignora.
public class SugestaoMetadadosDto
{
    public NivelBloom? Bloom { get; set; }

    public Dificuldade? Dificuldade { get; set; }

    public List<string> Tags { get; set; } = new();

    // Preenchido só quando o texto da IA bateu (case-insensitive) com um
    // Assunto cadastrado. Quando não bate, fica null e AssuntoRotulo mostra o texto cru como dica.
    public int? AssuntoId { get; set; }

    public string? AssuntoRotulo { get; set; }

    // Mesmo espírito de AssuntoId/AssuntoRotulo, mas pro item ENADE ativo apontado pela IA.
    public int? ItemMatrizId { get; set; }

    public string? ItemMatrizRotulo { get; set; }
}
