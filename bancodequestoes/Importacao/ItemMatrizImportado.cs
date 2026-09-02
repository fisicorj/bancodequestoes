using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Modelo "achatado" com o resultado de UM item de matriz interpretado de um
// arquivo CSV/XLSX/JSON — ainda não é uma entidade do banco, só o que a tela
// de pré-visualização (MatrizItensImportar.razor) e o botão "Importar"
// precisam. Mesmo espírito de QuestaoImportada (import Aiken/GIFT).
public class ItemMatrizImportado
{
    public required string Codigo { get; set; }
    public required string Titulo { get; set; }
    public string? Descricao { get; set; }
    public required TipoItemMatriz Tipo { get; set; }
    public int Ordem { get; set; }

    // Se algo ficou estranho mas ainda dá pra importar (ex.: "Tipo" da linha
    // não bateu com nenhum valor conhecido e caiu no padrão Outro), fica um
    // aviso em vez de descartar a linha inteira.
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

// Resultado "cru" de interpretar um JSON de MATRIZ COMPLETA (item 17 da 2ª
// rodada de revisão) — metadados da matriz (Curso/Tipo/Ano/Órgão/Documento)
// + os itens (reaproveitando ResultadoImportacaoItens, mesmo parser de
// grupos já usado pra importação só-de-itens). Ainda é só o que foi lido do
// arquivo, sem tocar no banco nem resolver Curso/Tipo pra valores de
// verdade — isso é MatrizReferenciaService.PrepararImportacaoMatrizCompletaAsync,
// que já precisa do banco pra localizar o Curso e detectar duplicata.
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
