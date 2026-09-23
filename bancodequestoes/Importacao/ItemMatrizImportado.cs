using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Modelo "achatado" com o resultado de UM item de matriz interpretado de CSV/XLSX/JSON
// — ainda não é entidade do banco, só o que a pré-visualização/importação precisam.
public class ItemMatrizImportado
{
    public required string Codigo { get; set; }
    public required string Titulo { get; set; }
    public string? Descricao { get; set; }
    public required TipoItemMatriz Tipo { get; set; }
    public int Ordem { get; set; }

    // Se algo ficou estranho mas ainda dá pra importar, fica um aviso em vez
    // de descartar a linha inteira.
    public string? Aviso { get; set; }

    // Selecionado por padrão na pré-visualização; o usuário pode desmarcar
    // antes de confirmar a importação.
    public bool Selecionada { get; set; } = true;
}

// Resultado completo de uma importação: itens reconhecidos + erros de linhas
// que não deu pra interpretar (mostrados à parte, sem travar o resto).
public class ResultadoImportacaoItens
{
    public List<ItemMatrizImportado> Itens { get; set; } = new();
    public List<string> Erros { get; set; } = new();
}

// Resultado "cru" de interpretar um JSON de MATRIZ COMPLETA: metadados da matriz +
// itens, ainda sem tocar no banco — resolver Curso/Tipo de verdade é MatrizReferenciaService.
public class MatrizCompletaImportada
{
    public string? Curso { get; set; }
    public string? Tipo { get; set; }
    public int? Ano { get; set; }
    public string? Edicao { get; set; }
    public string? Orgao { get; set; }
    public string? Documento { get; set; }
    public string? UrlFonte { get; set; }
    public string? Descricao { get; set; }
    public required ResultadoImportacaoItens Itens { get; set; }
}
