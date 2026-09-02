using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BancoQuestoes.Exportacao;

// Converte o Enunciado (Markdown + LaTeX) numa lista de blocos "prontos pra
// desenhar" (ver BlocoMarkdown), andando pela árvore de sintaxe do Markdig uma
// única vez. Os dois exportadores (DOCX, PDF) consomem o MESMO resultado desta
// classe — nenhum dos dois entende Markdig diretamente, só sabe iterar
// BlocoMarkdown/TrechoTexto. Isso evita ter duas implementações de parsing de
// Markdown divergindo aos poucos.
public static class MarkdownConversor
{
    // Negrito, itálico, lista e código já vêm de graça no CommonMark básico —
    // a única extensão que realmente precisamos ligar é a de tabelas (sintaxe
    // "| a | b |"). Deliberadamente NÃO usamos UseAdvancedExtensions(): esse
    // pacote inclui a extensão de Matemática, que passaria a interpretar
    // "$...$" como um nó de fórmula PRÓPRIO do Markdig (MathInline/MathBlock)
    // em vez de deixar como texto puro — e é o texto puro que o
    // FormulaRenderer (CodeCogs) sabe procurar e converter em imagem. Ligar
    // Math aqui quebraria silenciosamente todo o suporte a LaTeX existente.
    // DisableHtml(): o professor não deveria conseguir injetar HTML bruto no
    // enunciado (nem que seja sem querer, colando de outro lugar).
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    public static async Task<List<BlocoMarkdown>> ConverterAsync(string? markdown)
    {
        var resultado = new List<BlocoMarkdown>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return resultado;
        }

        var documento = Markdown.Parse(markdown, Pipeline);
        await ConverterBlocosAsync(documento, resultado);
        return resultado;
    }

    private static async Task ConverterBlocosAsync(ContainerBlock container, List<BlocoMarkdown> destino)
    {
        foreach (var bloco in container)
        {
            switch (bloco)
            {
                case Table tabela:
                    destino.Add(await ConverterTabelaAsync(tabela));
                    break;

                case ListBlock lista:
                    var numero = 1;
                    foreach (var itemBloco in lista)
                    {
                        if (itemBloco is not ListItemBlock item)
                        {
                            continue;
                        }
                        var trechos = await ConverterConteudoDeItemAsync(item);
                        destino.Add(new BlocoMarkdown
                        {
                            Tipo = lista.IsOrdered ? TipoBlocoMarkdown.ItemListaNumerada : TipoBlocoMarkdown.ItemListaComMarcador,
                            Trechos = trechos,
                            NumeroLista = numero++,
                        });
                    }
                    break;

                // FencedCodeBlock (```) e CodeBlock (indentado com 4 espaços) —
                // as duas formas do autor marcar "isto é código, não formate nada
                // aqui dentro". FencedCodeBlock herda de CodeBlock, então um "case
                // CodeBlock" já cobre as duas.
                case CodeBlock codigo:
                    destino.Add(new BlocoMarkdown { Tipo = TipoBlocoMarkdown.BlocoCodigo, CodigoBruto = codigo.Lines.ToString() });
                    break;

                case LeafBlock folha when folha.Inline is not null:
                    // Parágrafo, título (#), citação de uma linha etc. — qualquer
                    // bloco "folha" com conteúdo inline vira um parágrafo.
                    var trechosParagrafo = await ConverterInlineAsync(folha.Inline.FirstChild, negrito: false, italico: false);
                    if (trechosParagrafo.Count > 0)
                    {
                        destino.Add(new BlocoMarkdown { Tipo = TipoBlocoMarkdown.Paragrafo, Trechos = trechosParagrafo });
                    }
                    break;

                case ContainerBlock aninhado:
                    // Blockquote e qualquer outro contêiner sem tratamento
                    // específico: desce recursivamente em vez de descartar o
                    // conteúdo silenciosamente.
                    await ConverterBlocosAsync(aninhado, destino);
                    break;
            }
        }
    }

    private static async Task<List<TrechoTexto>> ConverterConteudoDeItemAsync(ListItemBlock item)
    {
        // Um item de lista normalmente tem um único parágrafo dentro. Uma lista
        // "solta" (com linha em branco entre itens) pode gerar mais de um bloco
        // por item — junta tudo num item só (não suportamos múltiplos parágrafos
        // dentro do mesmo item por ora).
        var trechos = new List<TrechoTexto>();
        foreach (var filho in item)
        {
            if (filho is LeafBlock folha && folha.Inline is not null)
            {
                if (trechos.Count > 0)
                {
                    trechos.Add(new TrechoTexto { Texto = " " });
                }
                trechos.AddRange(await ConverterInlineAsync(folha.Inline.FirstChild, negrito: false, italico: false));
            }
        }
        return trechos;
    }

    private static async Task<BlocoMarkdown> ConverterTabelaAsync(Table tabela)
    {
        var linhas = new List<List<List<TrechoTexto>>>();
        var temCabecalho = false;

        foreach (var linhaObj in tabela)
        {
            if (linhaObj is not TableRow linha)
            {
                continue;
            }
            if (linha.IsHeader)
            {
                temCabecalho = true;
            }

            var celulas = new List<List<TrechoTexto>>();
            foreach (var celulaObj in linha)
            {
                if (celulaObj is not TableCell celula)
                {
                    continue;
                }
                var trechos = new List<TrechoTexto>();
                foreach (var filho in celula)
                {
                    if (filho is LeafBlock folha && folha.Inline is not null)
                    {
                        trechos.AddRange(await ConverterInlineAsync(folha.Inline.FirstChild, negrito: false, italico: false));
                    }
                }
                celulas.Add(trechos);
            }
            linhas.Add(celulas);
        }

        return new BlocoMarkdown { Tipo = TipoBlocoMarkdown.Tabela, Tabela = linhas, TabelaTemCabecalho = temCabecalho };
    }

    // Anda pela lista encadeada de inlines (texto, negrito, itálico, código,
    // quebra de linha...) carregando o negrito/itálico "herdado" de fora pra
    // dentro (um <strong><em>...</em></strong> aninhado precisa das duas
    // marcações no texto final). Fórmulas ($...$) só são detectadas dentro de
    // texto literal puro — nunca dentro de um CodeInline.
    private static async Task<List<TrechoTexto>> ConverterInlineAsync(Inline? inicio, bool negrito, bool italico)
    {
        var resultado = new List<TrechoTexto>();
        for (var atual = inicio; atual is not null; atual = atual.NextSibling)
        {
            switch (atual)
            {
                case LiteralInline literal:
                    resultado.AddRange(await FormulaRenderer.DividirComFormatacaoAsync(literal.Content.ToString(), negrito, italico, codigoInline: false));
                    break;

                case CodeInline codigo:
                    // .ToString() funciona tanto se Content for string quanto
                    // StringSlice (versões diferentes do Markdig usam tipos
                    // diferentes aqui) — dessa forma funciona nos dois casos.
                    resultado.Add(new TrechoTexto { Texto = codigo.Content.ToString(), CodigoInline = true });
                    break;

                case EmphasisInline enfase:
                    // DelimiterCount: 1 = itálico (*/_), 2 = negrito (**/__),
                    // 3 = os dois juntos (***/___).
                    var novoNegrito = negrito || enfase.DelimiterCount is 2 or 3;
                    var novoItalico = italico || enfase.DelimiterCount is 1 or 3;
                    resultado.AddRange(await ConverterInlineAsync(enfase.FirstChild, novoNegrito, novoItalico));
                    break;

                case LineBreakInline:
                    resultado.Add(new TrechoTexto { Texto = "\n" });
                    break;

                case ContainerInline container:
                    // Link, e qualquer outro inline "com filhos" sem tratamento
                    // especial: desce recursivamente (perde a URL do link, mas
                    // mantém o texto visível em vez de sumir com ele).
                    resultado.AddRange(await ConverterInlineAsync(container.FirstChild, negrito, italico));
                    break;

                default:
                    // Autolink, entidade HTML etc. — mantém como texto puro em
                    // vez de simplesmente descartar o conteúdo.
                    var textoBruto = atual.ToString();
                    if (!string.IsNullOrEmpty(textoBruto))
                    {
                        resultado.AddRange(await FormulaRenderer.DividirComFormatacaoAsync(textoBruto, negrito, italico, codigoInline: false));
                    }
                    break;
            }
        }
        return resultado;
    }
}
