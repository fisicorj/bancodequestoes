using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BancoQuestoes.Exportacao;

// Converte o Enunciado (Markdown + LaTeX) numa lista de BlocoMarkdown, andando pela
// árvore do Markdig uma vez; DOCX e PDF consomem o mesmo resultado, sem duplicar parsing.
public static class MarkdownConversor
{
    // Só liga tabelas; NÃO usa UseAdvancedExtensions() pois sua extensão de Matemática
    // quebraria o LaTeX que o FormulaRenderer espera como texto puro.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    // resolverImagem: resolve a URL da imagem pros bytes prontos pra desenhar; null
    // (padrão ou URL não reconhecida) faz o parágrafo cair no tratamento normal de texto.
    public static async Task<List<BlocoMarkdown>> ConverterAsync(string? markdown, Func<string, Task<ImagemExportDto?>>? resolverImagem = null)
    {
        var resultado = new List<BlocoMarkdown>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return resultado;
        }

        var documento = Markdown.Parse(markdown, Pipeline);
        await ConverterBlocosAsync(documento, resultado, resolverImagem);
        return resultado;
    }

    private static async Task ConverterBlocosAsync(ContainerBlock container, List<BlocoMarkdown> destino, Func<string, Task<ImagemExportDto?>>? resolverImagem)
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

                // FencedCodeBlock e CodeBlock indentado: FencedCodeBlock herda de
                // CodeBlock, então "case CodeBlock" já cobre as duas formas.
                case CodeBlock codigo:
                    destino.Add(new BlocoMarkdown { Tipo = TipoBlocoMarkdown.BlocoCodigo, CodigoBruto = codigo.Lines.ToString() });
                    break;

                case LeafBlock folha when folha.Inline is not null:
                    // Parágrafo cujo único conteúdo é uma imagem (ver ObterImagemUnica)
                    // vira bloco Imagem; precisa vir antes do tratamento genérico abaixo.
                    if (resolverImagem is not null && ObterImagemUnica(folha.Inline.FirstChild) is { } imagemLink)
                    {
                        var resolvida = await resolverImagem(imagemLink.Url ?? "");
                        if (resolvida is not null)
                        {
                            destino.Add(new BlocoMarkdown { Tipo = TipoBlocoMarkdown.Imagem, Imagem = resolvida });
                            break;
                        }
                    }

                    // Qualquer bloco "folha" com conteúdo inline vira um parágrafo.
                    var trechosParagrafo = await ConverterInlineAsync(folha.Inline.FirstChild, negrito: false, italico: false);
                    if (trechosParagrafo.Count > 0)
                    {
                        destino.Add(new BlocoMarkdown { Tipo = TipoBlocoMarkdown.Paragrafo, Trechos = trechosParagrafo });
                    }
                    break;

                case ContainerBlock aninhado:
                    // Blockquote e outros contêineres sem tratamento específico:
                    // desce recursivamente em vez de descartar o conteúdo.
                    await ConverterBlocosAsync(aninhado, destino, resolverImagem);
                    break;
            }
        }
    }

    // Detecta parágrafo cujo único conteúdo visível é uma imagem sozinha na linha
    // (como o botão "Imagem" do EnunciadoEditor insere); outro conteúdo invalida.
    private static LinkInline? ObterImagemUnica(Inline? inicio)
    {
        LinkInline? encontrada = null;
        for (var atual = inicio; atual is not null; atual = atual.NextSibling)
        {
            if (atual is LinkInline { IsImage: true } link)
            {
                if (encontrada is not null)
                {
                    return null; // mais de uma imagem no parágrafo — não é o caso simples
                }
                encontrada = link;
                continue;
            }
            if (atual is LiteralInline literalVazio && string.IsNullOrWhiteSpace(literalVazio.Content.ToString()))
            {
                continue;
            }
            return null;
        }
        return encontrada;
    }

    private static async Task<List<TrechoTexto>> ConverterConteudoDeItemAsync(ListItemBlock item)
    {
        // Uma lista "solta" pode gerar mais de um bloco por item; junta tudo num
        // item só (não suportamos múltiplos parágrafos dentro do mesmo item).
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

    // Anda pela lista de inlines carregando negrito/itálico herdado de fora pra
    // dentro. Fórmulas ($...$) só são detectadas em texto literal puro, nunca em CodeInline.
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
                    // .ToString() funciona tanto se Content for string quanto StringSlice.
                    resultado.Add(new TrechoTexto { Texto = codigo.Content.ToString(), CodigoInline = true });
                    break;

                case EmphasisInline enfase:
                    // DelimiterCount: 1 = itálico, 2 = negrito, 3 = os dois juntos.
                    var novoNegrito = negrito || enfase.DelimiterCount is 2 or 3;
                    var novoItalico = italico || enfase.DelimiterCount is 1 or 3;
                    resultado.AddRange(await ConverterInlineAsync(enfase.FirstChild, novoNegrito, novoItalico));
                    break;

                case LineBreakInline:
                    resultado.Add(new TrechoTexto { Texto = "\n" });
                    break;

                case ContainerInline container:
                    // Link e outros inlines "com filhos": desce recursivamente,
                    // perde a URL mas mantém o texto visível.
                    resultado.AddRange(await ConverterInlineAsync(container.FirstChild, negrito, italico));
                    break;

                default:
                    // Autolink, entidade HTML etc.: mantém como texto puro em vez de descartar.
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
