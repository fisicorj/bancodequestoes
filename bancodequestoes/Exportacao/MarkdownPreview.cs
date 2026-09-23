using System.Text.RegularExpressions;
using BancoQuestoes.Services;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BancoQuestoes.Exportacao;

// Pipeline Markdown -> HTML pra pré-visualização (diferente da exportação DOCX/PDF,
// que usa MarkdownConversor); sem UseAdvancedExtensions() pra não brigar com "$...$" do FormulaRenderer.
//
// Esse HTML vira MarkupString em vários componentes (EnunciadoEditor, ExplicacaoEditor,
// DiscursivaEditor, QuestaoPreviewModal), o que desliga o escape automático do Blazor.
// DisableHtml() acima já bloqueia HTML bruto colado no enunciado, mas NÃO filtra o esquema
// de um link Markdown normal — "[clique](javascript:...)" é sintaxe válida e passaria direto.
// SanitizarLinks (abaixo) neutraliza qualquer link/imagem com esquema fora da allowlist antes
// de gerar o HTML.
public static class MarkdownPreview
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    // Reconhece as referências que o botão "Imagem" insere no enunciado:
    // "imagem:existente:{id}" (salva) ou "imagem:pendente:{token}" (upload ainda não salvo).
    private static readonly Regex ReferenciaImagemRegex = new(@"imagem:(pendente|existente):([A-Za-z0-9_-]+)", RegexOptions.Compiled);

    public static string ParaHtml(string? markdown, List<PendenteImagem>? pendentes = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "";
        }

        // Reescreve a referência pra URL que o navegador resolve ANTES de converter:
        // "existente" vira endpoint de QuestaoImagem, "pendente" vira data-URI em memória.
        var comUrlsResolvidas = ReferenciaImagemRegex.Replace(markdown, m =>
        {
            var tipo = m.Groups[1].Value;
            var chave = m.Groups[2].Value;
            if (tipo == "existente")
            {
                return $"questoes/imagem/{chave}";
            }
            var pendente = pendentes?.FirstOrDefault(p => p.Token == chave);
            return pendente is null
                ? m.Value
                : $"data:{pendente.ContentType};base64,{Convert.ToBase64String(pendente.Conteudo)}";
        });

        // Parse + render em duas etapas (em vez de Markdown.ToHtml de uma vez) pra poder
        // andar pela árvore e neutralizar links perigosos antes de virar HTML.
        var documento = Markdown.Parse(comUrlsResolvidas, Pipeline);
        SanitizarLinks(documento);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(documento);
        return writer.ToString();
    }

    // Anda pela árvore trocando o Url de qualquer LinkInline (normal ou imagem) que não
    // esteja na allowlist de esquemas por "#" — mantém o texto visível do link, só desarma
    // o destino. Cobre tanto link solto quanto link aninhado dentro de negrito/itálico/etc.
    private static void SanitizarLinks(ContainerBlock container)
    {
        foreach (var bloco in container)
        {
            if (bloco is ContainerBlock aninhado)
            {
                SanitizarLinks(aninhado);
            }
            else if (bloco is LeafBlock folha && folha.Inline is not null)
            {
                SanitizarInlines(folha.Inline.FirstChild);
            }
        }
    }

    private static void SanitizarInlines(Inline? inicio)
    {
        for (var atual = inicio; atual is not null; atual = atual.NextSibling)
        {
            if (atual is LinkInline link && !UrlEhSegura(link.Url, link.IsImage))
            {
                link.Url = "#";
            }
            if (atual is ContainerInline containerInline)
            {
                SanitizarInlines(containerInline.FirstChild);
            }
        }
    }

    // http/https sempre; mailto só em link de texto (não faz sentido em <img src>); data:
    // só em imagem (é assim que as imagens "pendente" — ainda não salvas — chegam aqui, como
    // data URI local montada por este mesmo arquivo, nunca vindo direto de input externo).
    // URL sem esquema (relativa, tipo "questoes/imagem/5" ou "#ancora") é sempre segura.
    private static bool UrlEhSegura(string? url, bool imagem)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return true;
        }
        var esquema = uri.Scheme.ToLowerInvariant();
        return esquema switch
        {
            "http" or "https" => true,
            "mailto" => !imagem,
            "data" => imagem,
            _ => false,
        };
    }
}
