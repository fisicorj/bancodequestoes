namespace BancoQuestoes.Exportacao;

public enum TipoBlocoMarkdown
{
    Paragrafo,
    ItemListaComMarcador,
    ItemListaNumerada,
    BlocoCodigo,
    Tabela,

    // Parágrafo cujo ÚNICO conteúdo é uma imagem sozinha na própria linha; imagem
    // misturada com texto na mesma linha vira Paragrafo normal (não suportado ainda).
    Imagem,
}

// Representação "achatada" de um enunciado convertido de Markdown, pronta pra
// qualquer exportador desenhar sem entender a árvore de sintaxe do Markdig.
public sealed class BlocoMarkdown
{
    public TipoBlocoMarkdown Tipo { get; set; }

    // Paragrafo / ItemLista*: texto já resolvido (negrito/itálico/código/fórmula).
    public List<TrechoTexto> Trechos { get; set; } = new();

    // ItemListaNumerada: número do item (1, 2, 3...) já calculado.
    public int NumeroLista { get; set; }

    // BlocoCodigo: conteúdo bruto, sem NENHUMA formatação/fórmula — dentro de
    // código, tudo é texto literal (inclusive "$", que em outro lugar viraria fórmula).
    public string? CodigoBruto { get; set; }

    // Tabela: linhas -> células -> trechos de cada célula.
    public List<List<List<TrechoTexto>>>? Tabela { get; set; }
    public bool TabelaTemCabecalho { get; set; }

    // Imagem: dados já resolvidos (bytes + metadados) pelo ProvaExportLoader;
    // reaproveita ImagemExportDto em vez de um tipo novo.
    public ImagemExportDto? Imagem { get; set; }
}
