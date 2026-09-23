using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Resultado de "gerar questão a partir de material": diferente de
// SugestaoMetadadosDto (só metadados), carrega uma questão inteira gerada do zero.
public class QuestaoGeradaDto
{
    public string Enunciado { get; set; } = "";

    // Sempre 4 alternativas (só Múltipla Escolha é gerada nesta versão).
    public List<string> Alternativas { get; set; } = new();

    public int RespostaCorretaIndex { get; set; }

    public NivelBloom? Bloom { get; set; }

    public List<string> Tags { get; set; } = new();

    // Só preenchido quando o rótulo da IA bateu (case-insensitive) com um Assunto real da Disciplina.
    public int? AssuntoId { get; set; }

    public string? AssuntoRotulo { get; set; }
}
