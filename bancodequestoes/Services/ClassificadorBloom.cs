using System.Text.RegularExpressions;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Heurística simples (sem IA) pra sugerir o nível de Bloom mais provável de
// uma questão a partir do verbo/comando do enunciado — não é ciência exata,
// é só um ponto de partida pra reduzir trabalho manual. QuestaoService só usa
// isso pra preencher questões que ainda estão SEM Bloom; o professor revisa
// depois em QuestaoForm normalmente.
//
// Ordem de checagem: do nível mais "sofisticado" (Criar) pro mais básico
// (Lembrar) — verbos de nível alto são mais específicos e raros, então
// checá-los primeiro evita que uma palavra genérica tipo "explique" (que
// pode aparecer solta em qualquer canto do enunciado) mascare um comando
// mais preciso que também esteja presente.
public static class ClassificadorBloom
{
    private static readonly (NivelBloom Nivel, string[] Palavras)[] Niveis =
    {
        (NivelBloom.Criar, new[]
        {
            "crie", "criar", "elabore", "elaborar", "proponha", "propor", "desenvolva", "desenvolver",
            "formule", "formular", "projete", "projetar", "invente", "inventar", "componha", "compor",
            "planeje", "planejar", "desenhe uma solução",
        }),
        (NivelBloom.Avaliar, new[]
        {
            "avalie", "avaliar", "julgue", "julgar", "critique", "criticar", "justifique", "justificar",
            "argumente", "argumentar", "defenda", "defender", "posicione-se", "opine", "opinar",
            "recomende", "recomendar", "qual é melhor",
        }),
        (NivelBloom.Analisar, new[]
        {
            "compare e contraste", "diferencie", "diferenciar", "analise", "analisar", "categorize",
            "categorizar", "investigue", "investigar", "examine", "examinar", "distinga", "distinguir",
            "quais são as diferenças", "identifique as causas",
        }),
        (NivelBloom.Aplicar, new[]
        {
            "calcule", "calcular", "resolva", "resolver", "demonstre", "demonstrar", "aplique", "aplicar",
            "construa", "construir", "execute", "executar", "utilize", "utilizar", "implemente",
            "implementar", "converta", "converter", "determine", "determinar",
        }),
        (NivelBloom.Entender, new[]
        {
            "explique", "explicar", "resuma", "resumir", "interprete", "interpretar", "compare", "comparar",
            "classifique", "classificar", "exemplifique", "exemplificar", "dê um exemplo", "traduza",
            "traduzir", "o que significa", "por que",
        }),
        (NivelBloom.Lembrar, new[]
        {
            "defina", "definir", "liste", "listar", "cite", "citar", "identifique", "identificar",
            "nomeie", "nomear", "descreva", "descrever", "o que é", "qual é", "quais são", "enumere",
            "enumerar",
        }),
    };

    // Retorna null quando nenhuma palavra-chave bate — melhor deixar sem
    // sugestão do que "chutar" um nível errado.
    public static NivelBloom? Classificar(string enunciado)
    {
        foreach (var (nivel, palavras) in Niveis)
        {
            foreach (var palavra in palavras)
            {
                if (Regex.IsMatch(enunciado, $@"\b{Regex.Escape(palavra)}\b", RegexOptions.IgnoreCase))
                {
                    return nivel;
                }
            }
        }

        return null;
    }
}
