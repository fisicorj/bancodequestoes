using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Interpreta um arquivo CSV/XLSX/JSON com itens de uma Matriz de Referência
// (Perfil do Concluinte/Competências/Conteúdos/...) — só INTERPRETA, nunca
// toca no banco (isso é MatrizReferenciaService.ImportarItensAsync). Mesmo
// espírito/separação do antigo DiretrizImportParser (recurso anterior,
// removido) — reaproveita inclusive a mesma técnica de leitura de .xlsx via
// DocumentFormat.OpenXml, já usada no projeto pra exportação DOCX.
//
// Diferente do antigo parser de Diretriz, não existe CodigoPai pra resolver
// (itens são flat), então não tem duas passadas nem checagem de ciclo — cada
// linha vira um ItemMatrizImportado independente.
public static class ItemMatrizImportParser
{
    // --- CSV ---
    //
    // Cabeçalho obrigatório na primeira linha, com estas colunas (qualquer
    // ordem, case-insensitive): Codigo, Titulo, Tipo, Descricao (opcional),
    // Ordem (opcional). Separador vírgula, com suporte a campos entre aspas
    // (pra texto que já contenha vírgula).
    public static ResultadoImportacaoItens ParseCsv(string texto)
    {
        var resultado = new ResultadoImportacaoItens();
        var linhas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => l.Length > 0)
            .ToList();

        if (linhas.Count == 0)
        {
            resultado.Erros.Add("Arquivo vazio.");
            return resultado;
        }

        var cabecalho = DividirLinhaCsv(linhas[0]).Select(c => c.Trim()).ToList();
        var indices = MapearColunas(cabecalho);

        if (indices is null)
        {
            resultado.Erros.Add("Cabeçalho do CSV precisa ter as colunas \"Codigo\", \"Titulo\" e \"Tipo\" (Descricao e Ordem são opcionais).");
            return resultado;
        }

        for (var i = 1; i < linhas.Count; i++)
        {
            var campos = DividirLinhaCsv(linhas[i]);
            if (campos.All(string.IsNullOrWhiteSpace))
            {
                continue; // linha em branco no meio do arquivo — ignora silenciosamente
            }

            ProcessarLinha(
                numeroLinha: i + 1,
                codigo: CampoOuVazio(campos, indices.Value.Codigo),
                titulo: CampoOuVazio(campos, indices.Value.Titulo),
                tipoTexto: CampoOuVazio(campos, indices.Value.Tipo),
                descricao: CampoOuVazio(campos, indices.Value.Descricao),
                ordemTexto: CampoOuVazio(campos, indices.Value.Ordem),
                resultado: resultado);
        }

        return resultado;
    }

    private static (int Codigo, int Titulo, int Tipo, int Descricao, int Ordem)? MapearColunas(List<string> cabecalho)
    {
        int Indice(params string[] nomes) =>
            cabecalho.FindIndex(c => nomes.Any(n => string.Equals(c, n, StringComparison.OrdinalIgnoreCase)));

        var codigo = Indice("Codigo", "Código");
        var titulo = Indice("Titulo", "Título");
        var tipo = Indice("Tipo");

        if (codigo < 0 || titulo < 0 || tipo < 0)
        {
            return null;
        }

        return (codigo, titulo, tipo, Indice("Descricao", "Descrição"), Indice("Ordem"));
    }

    private static string CampoOuVazio(List<string> campos, int indice) =>
        indice >= 0 && indice < campos.Count ? campos[indice].Trim() : "";

    // Parser CSV simples que respeita aspas (campo entre aspas pode conter
    // vírgula; "" dentro de um campo entre aspas vira um " literal) — não
    // pretende ser um parser CSV genérico completo (RFC 4180 inteiro), só o
    // suficiente pra planilhas exportadas do Excel/Google Sheets.
    private static List<string> DividirLinhaCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];

            if (dentroDeAspas)
            {
                if (c == '"' && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    dentroDeAspas = false;
                }
                else
                {
                    atual.Append(c);
                }
            }
            else if (c == '"')
            {
                dentroDeAspas = true;
            }
            else if (c == ',')
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos;
    }

    // --- XLSX ---
    //
    // Mesmas colunas do CSV, na primeira planilha do arquivo, primeira linha
    // como cabeçalho.
    public static ResultadoImportacaoItens ParseXlsx(Stream arquivo)
    {
        var resultado = new ResultadoImportacaoItens();

        using var documento = SpreadsheetDocument.Open(arquivo, false);
        var workbookPart = documento.WorkbookPart;
        var planilha = workbookPart?.Workbook.Descendants<Sheet>().FirstOrDefault();
        if (workbookPart is null || planilha?.Id?.Value is null)
        {
            resultado.Erros.Add("Não foi possível ler nenhuma planilha do arquivo .xlsx.");
            return resultado;
        }

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(planilha.Id.Value!);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var linhas = worksheetPart.Worksheet.Descendants<Row>().ToList();

        if (linhas.Count == 0)
        {
            resultado.Erros.Add("Planilha vazia.");
            return resultado;
        }

        string ValorCelula(Cell? celula)
        {
            if (celula?.CellValue is null)
            {
                return "";
            }

            var texto = celula.CellValue.InnerText;

            // Célula de texto "compartilhado" (o padrão do Excel pra economizar
            // espaço) guarda só um ÍNDICE na célula — o texto de verdade está
            // na SharedStringTable do workbook, não na célula. Elements<>() é
            // necessário aqui: SharedStringTable não é diretamente indexável
            // por posição via ElementAtOrDefault sem passar por Elements<T>().
            if (celula.DataType?.Value == CellValues.SharedString && sharedStrings is not null
                && int.TryParse(texto, out var indice))
            {
                return sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(indice)?.InnerText ?? "";
            }

            return texto;
        }

        List<string> ValoresDaLinha(Row linha, int quantidadeColunas)
        {
            var celulas = linha.Elements<Cell>().ToList();
            var valores = new List<string>(new string[quantidadeColunas]);
            for (var i = 0; i < valores.Count; i++)
            {
                valores[i] = "";
            }

            foreach (var celula in celulas)
            {
                var indiceColuna = IndiceColunaDaReferencia(celula.CellReference?.Value);
                if (indiceColuna is int idx && idx < quantidadeColunas)
                {
                    valores[idx] = ValorCelula(celula);
                }
            }

            return valores;
        }

        var cabecalhoBruto = ValoresDaLinha(linhas[0], linhas[0].Elements<Cell>().Count());
        var quantidadeColunas = Math.Max(cabecalhoBruto.Count, 5);
        var cabecalho = ValoresDaLinha(linhas[0], quantidadeColunas);
        var indices = MapearColunas(cabecalho);

        if (indices is null)
        {
            resultado.Erros.Add("Cabeçalho da planilha precisa ter as colunas \"Codigo\", \"Titulo\" e \"Tipo\" (Descricao e Ordem são opcionais).");
            return resultado;
        }

        for (var i = 1; i < linhas.Count; i++)
        {
            var valores = ValoresDaLinha(linhas[i], quantidadeColunas);
            if (valores.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            ProcessarLinha(
                numeroLinha: i + 1,
                codigo: CampoOuVazio(valores, indices.Value.Codigo),
                titulo: CampoOuVazio(valores, indices.Value.Titulo),
                tipoTexto: CampoOuVazio(valores, indices.Value.Tipo),
                descricao: CampoOuVazio(valores, indices.Value.Descricao),
                ordemTexto: CampoOuVazio(valores, indices.Value.Ordem),
                resultado: resultado);
        }

        return resultado;
    }

    // "B7" -> 1 (coluna B, base zero) — usado porque células vazias no meio
    // de uma linha do .xlsx às vezes simplesmente não aparecem no XML, então
    // não dá pra confiar na posição da célula dentro de Row.Elements<Cell>().
    private static int? IndiceColunaDaReferencia(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia))
        {
            return null;
        }

        var letras = new string(referencia.TakeWhile(char.IsLetter).ToArray());
        if (letras.Length == 0)
        {
            return null;
        }

        var indice = 0;
        foreach (var c in letras.ToUpperInvariant())
        {
            indice = indice * 26 + (c - 'A' + 1);
        }

        return indice - 1;
    }

    // --- JSON ---
    //
    // Aceita duas formas:
    //  1) Uma lista simples: [{"codigo":"C01","tipo":"Competencia","titulo":"...","descricao":"...","ordem":1}, ...]
    //  2) O formato "matriz completa" (pensado pra futura criação de matriz +
    //     itens num só arquivo): {"grupos":{"perfil":[...],"competencias":[...],
    //     "conteudos":[...],"habilidades":[...],"objetosConhecimento":[...]}} —
    //     cada item dentro de um grupo só precisa de codigo/titulo/descricao;
    //     o Tipo é inferido do nome do grupo.
    public static ResultadoImportacaoItens ParseJson(string texto)
    {
        var resultado = new ResultadoImportacaoItens();

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(texto);
        }
        catch (JsonException ex)
        {
            resultado.Erros.Add($"JSON inválido: {ex.Message}");
            return resultado;
        }

        using (documento)
        {
            var raiz = documento.RootElement;

            if (raiz.ValueKind == JsonValueKind.Array)
            {
                var ordem = 0;
                foreach (var elemento in raiz.EnumerateArray())
                {
                    ProcessarObjetoJson(elemento, ordem++, tipoForcado: null, resultado);
                }
                return resultado;
            }

            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("grupos", out var grupos) && grupos.ValueKind == JsonValueKind.Object)
            {
                ProcessarGrupoJson(grupos, "perfil", TipoItemMatriz.PerfilConcluinte, resultado);
                ProcessarGrupoJson(grupos, "competencias", TipoItemMatriz.Competencia, resultado);
                ProcessarGrupoJson(grupos, "conteudos", TipoItemMatriz.Conteudo, resultado);
                ProcessarGrupoJson(grupos, "habilidades", TipoItemMatriz.Habilidade, resultado);
                ProcessarGrupoJson(grupos, "objetosConhecimento", TipoItemMatriz.ObjetoConhecimento, resultado);
                ProcessarGrupoJson(grupos, "outros", TipoItemMatriz.Outro, resultado);
                return resultado;
            }

            resultado.Erros.Add("JSON precisa ser uma lista de itens ou um objeto com uma propriedade \"grupos\" (perfil/competencias/conteudos/...).");
            return resultado;
        }
    }

    // --- JSON de MATRIZ COMPLETA (item 17 da 2ª rodada de revisão) ---
    //
    // Formato: {"curso":"...","tipo":"ENADE","ano":2023,"edicao":"...",
    // "orgao":"INEP","documento":"...","urlFonte":"...","descricao":"...",
    // "grupos":{"perfil":[...],"competencias":[...],"conteudos":[...]}} — os
    // metadados no topo (curso/tipo/ano/...) são NOVOS em relação ao formato
    // "só grupos" que ParseJson já aceitava; os itens dentro de "grupos"
    // usam o MESMO ProcessarGrupoJson de sempre (nada duplicado). Sempre
    // devolve um objeto (nunca lança) — erros de parse viram
    // MatrizCompletaImportada.Itens.Erros, mesmo padrão dos outros parsers
    // desta classe, pra tela de preview mostrar tudo de um jeito só.
    public static MatrizCompletaImportada ParseMatrizCompleta(string texto)
    {
        var resultadoItens = new ResultadoImportacaoItens();
        var completa = new MatrizCompletaImportada { Itens = resultadoItens };

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(texto);
        }
        catch (JsonException ex)
        {
            resultadoItens.Erros.Add($"JSON inválido: {ex.Message}");
            return completa;
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                resultadoItens.Erros.Add("Esperado um objeto JSON com os campos da matriz (curso, tipo, ano, orgao, documento, ...) e uma propriedade \"grupos\".");
                return completa;
            }

            string? TextoOpcional(string propriedade) =>
                raiz.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                    ? v.GetString()!.Trim()
                    : null;

            completa.Curso = TextoOpcional("curso");
            completa.Tipo = TextoOpcional("tipo");
            completa.Edicao = TextoOpcional("edicao");
            completa.Orgao = TextoOpcional("orgao");
            completa.Documento = TextoOpcional("documento");
            completa.UrlFonte = TextoOpcional("urlFonte");
            completa.Descricao = TextoOpcional("descricao");
            completa.Ano = raiz.TryGetProperty("ano", out var anoEl) && anoEl.ValueKind == JsonValueKind.Number && anoEl.TryGetInt32(out var ano)
                ? ano
                : null;

            if (string.IsNullOrWhiteSpace(completa.Curso))
            {
                resultadoItens.Erros.Add("Falta o campo \"curso\" (nome do curso, usado pra localizar o curso correspondente no sistema).");
            }

            if (!raiz.TryGetProperty("grupos", out var grupos) || grupos.ValueKind != JsonValueKind.Object)
            {
                resultadoItens.Erros.Add("Falta a propriedade \"grupos\" (perfil/competencias/conteudos/...) com os itens da matriz.");
                return completa;
            }

            ProcessarGrupoJson(grupos, "perfil", TipoItemMatriz.PerfilConcluinte, resultadoItens);
            ProcessarGrupoJson(grupos, "competencias", TipoItemMatriz.Competencia, resultadoItens);
            ProcessarGrupoJson(grupos, "conteudos", TipoItemMatriz.Conteudo, resultadoItens);
            ProcessarGrupoJson(grupos, "habilidades", TipoItemMatriz.Habilidade, resultadoItens);
            ProcessarGrupoJson(grupos, "objetosConhecimento", TipoItemMatriz.ObjetoConhecimento, resultadoItens);
            ProcessarGrupoJson(grupos, "outros", TipoItemMatriz.Outro, resultadoItens);
        }

        return completa;
    }

    private static void ProcessarGrupoJson(JsonElement grupos, string nomePropriedade, TipoItemMatriz tipo, ResultadoImportacaoItens resultado)
    {
        if (!grupos.TryGetProperty(nomePropriedade, out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var ordem = 0;
        foreach (var elemento in lista.EnumerateArray())
        {
            ProcessarObjetoJson(elemento, ordem++, tipo, resultado);
        }
    }

    private static void ProcessarObjetoJson(JsonElement elemento, int ordemPadrao, TipoItemMatriz? tipoForcado, ResultadoImportacaoItens resultado)
    {
        string Texto(string propriedade) =>
            elemento.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        var codigo = Texto("codigo");
        var titulo = Texto("titulo");
        var descricao = Texto("descricao");
        var tipoTexto = tipoForcado is not null ? tipoForcado.Value.ToString() : Texto("tipo");
        var ordem = elemento.TryGetProperty("ordem", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : ordemPadrao;

        ProcessarLinha(numeroLinha: null, codigo, titulo, tipoTexto, descricao, ordem.ToString(), resultado);
    }

    // --- Comum aos três formatos ---

    private static void ProcessarLinha(int? numeroLinha, string codigo, string titulo, string tipoTexto, string descricao, string ordemTexto, ResultadoImportacaoItens resultado)
    {
        var prefixoErro = numeroLinha is int n ? $"Linha {n}: " : "";

        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(titulo))
        {
            resultado.Erros.Add($"{prefixoErro}faltando \"Codigo\" ou \"Titulo\" — linha ignorada.");
            return;
        }

        var (tipo, avisoTipo) = ResolverTipo(tipoTexto);
        var ordem = int.TryParse(ordemTexto, out var v) ? v : 0;

        resultado.Itens.Add(new ItemMatrizImportado
        {
            Codigo = codigo.Trim(),
            Titulo = titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            Tipo = tipo,
            Ordem = ordem,
            Aviso = avisoTipo,
        });
    }

    // Aceita tanto o nome do enum (ex.: "Competencia") quanto o rótulo em
    // português (ex.: "Competência", "Competências") — planilhas feitas à
    // mão tendem a usar o rótulo, exportações programáticas tendem a usar o
    // nome do enum. Cai em "Outro" com aviso quando não reconhece, em vez de
    // rejeitar a linha inteira — o professor ainda pode corrigir o Tipo
    // depois, direto na tela de gestão de itens.
    private static (TipoItemMatriz Tipo, string? Aviso) ResolverTipo(string tipoTexto)
    {
        var texto = tipoTexto.Trim();

        if (Enum.TryParse<TipoItemMatriz>(texto, ignoreCase: true, out var porNome))
        {
            return (porNome, null);
        }

        var normalizado = texto.Trim().TrimEnd('s').ToLowerInvariant();
        foreach (var candidato in Enum.GetValues<TipoItemMatriz>())
        {
            var rotulo = candidato.Rotulo().ToLowerInvariant();
            if (rotulo == texto.ToLowerInvariant() || rotulo.TrimEnd('s') == normalizado)
            {
                return (candidato, null);
            }
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return (TipoItemMatriz.Outro, "Tipo não informado — classificado como \"Outro\"; ajuste depois se necessário.");
        }

        return (TipoItemMatriz.Outro, $"Tipo \"{texto}\" não reconhecido — classificado como \"Outro\"; ajuste depois se necessário.");
    }
}
