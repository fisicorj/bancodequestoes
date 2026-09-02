namespace BancoQuestoes.Services;

using BancoQuestoes.Models;

// "Command"/DTO da tela de Matriz de Referência (MatrizForm.razor) — mesmo
// espírito de CursoInput/QuestaoInput: a tela liga um objeto público deste
// tipo, e quem decide como validar/persistir é o Service.
public sealed class MatrizReferenciaInput
{
    // Exatamente um dos dois é usado, dependendo do Tipo (ver
    // TipoMatrizReferenciaExtensions.EhEscopoNacional e o comentário de
    // escopo duplo em MatrizReferencia) — CursoId pra PPC/Institucional/
    // Outro, AreaCursoId pra ENADE/DCN. Validado em
    // MatrizReferenciaService.ValidarMatriz, não aqui (mesmo estilo dos
    // outros Inputs do projeto).
    public int CursoId { get; set; }
    public int? AreaCursoId { get; set; }
    public string Nome { get; set; } = "";
    public TipoMatrizReferencia Tipo { get; set; } = TipoMatrizReferencia.ENADE;
    public int? Ano { get; set; }
    public string? Edicao { get; set; }
    public string? Orgao { get; set; }
    public string? Documento { get; set; }
    public string? UrlFonte { get; set; }
    public string? Descricao { get; set; }
    public StatusMatrizReferencia Status { get; set; } = StatusMatrizReferencia.Rascunho;
}

// DTO da tela de item (dentro de MatrizItensPage.razor) — Codigo é sempre
// editável (item 7 do pedido: "não assumir que sempre será C01, C02 etc.").
public sealed class ItemMatrizInput
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string? Descricao { get; set; }
    public TipoItemMatriz Tipo { get; set; } = TipoItemMatriz.Competencia;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
}
