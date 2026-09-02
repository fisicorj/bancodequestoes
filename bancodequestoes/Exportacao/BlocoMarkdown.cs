namespace BancoQuestoes.Exportacao;

public enum TipoBlocoMarkdown
{
    Paragrafo,
    ItemListaComMarcador,
    ItemListaNumerada,
    BlocoCodigo,
    Tabela,
}

// Representação "achatada" de um enunciado já convertido de Markdown (ver
// MarkdownConversor), pronta pra qualquer exportador (DOCX, PDF) desenhar sem
// precisar entender a árvore de sintaxe do Markdig — só itera essa lista e
// trata cada tipo de bloco à sua maneira (parágrafo, item de lista, bloco de
// código, tabela).
public sealed class BlocoMarkdown
{
    public TipoBlocoMarkdown Tipo { get; set; }

    // Paragrafo / ItemLista*: texto já resolvido (negrito/itálico/código/fórmula).
    public List<TrechoTexto> Trechos { get; set; } = new();

    // ItemListaNumerada: número do item (1, 2, 3...) já calculado.
    public int NumeroLista { get; set; }

    // BlocoCodigo: conteúdo bruto, sem NENHUMA formatação nem fórmula aplicada —
    // dentro de um bloco de código, tudo é texto literal (inclusive um "$" que
    // em outro lugar viraria fórmula).
    public string? CodigoBruto { get; set; }

    // Tabela: linhas -> células -> trechos de cada célula.
    public List<List<List<TrechoTexto>>>? Tabela { get; set; }
    public bool TabelaTemCabecalho { get; set; }
}
