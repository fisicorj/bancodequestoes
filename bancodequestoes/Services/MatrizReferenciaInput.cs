namespace BancoQuestoes.Services;

using BancoQuestoes.Models;

// DTO da tela de Matriz de Referência (MatrizForm.razor); validação e persistência ficam no Service.
public sealed class MatrizReferenciaInput
{
    // Exatamente um dos dois é usado conforme o Tipo: CursoId pra
    // PPC/Institucional/Outro, AreaCursoId pra ENADE/DCN.
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
