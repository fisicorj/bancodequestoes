using Markdig;

namespace BancoQuestoes.Exportacao;

// Pipeline de Markdown -> HTML compartilhado entre a pré-visualização ao vivo
// do formulário de questão e o modal de prévia da listagem — os dois só
// precisam de HTML pronto pra tela (diferente da exportação DOCX/PDF, que
// precisa da árvore de blocos "achatada" via MarkdownConversor). Mesma
// configuração dos dois lugares: só a extensão de tabelas, sem
// UseAdvancedExtensions() (que ligaria a extensão de Matemática do Markdig e
// tomaria conta do "$...$" antes do MathJax/FormulaRenderer conseguirem).
public static class MarkdownPreview
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    public static string ParaHtml(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, Pipeline);
}
