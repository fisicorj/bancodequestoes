using BancoQuestoes.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Dw = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;

namespace BancoQuestoes.Exportacao;

public static class ProvaDocxExporter
{
    public static byte[] Gerar(ProvaExportDto prova)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            EscreverVersao(mainPart, body, prova, prova.Questoes, versionLabel: null);

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // Gera N versões (A, B, C...) com questões/alternativas embaralhadas, seguidas
    // de uma página de gabarito com a resposta de cada versão.
    public static byte[] GerarVariacoes(ProvaExportDto prova, int quantidadeVersoes)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var versoes = new List<(string Rotulo, List<QuestaoExportDto> Questoes)>();

            for (var i = 0; i < quantidadeVersoes; i++)
            {
                var rotulo = ((char)('A' + i)).ToString();
                var questoesEmbaralhadas = EmbaralharQuestoes(prova.Questoes);
                versoes.Add((rotulo, questoesEmbaralhadas));

                if (i > 0)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                EscreverVersao(mainPart, body, prova, questoesEmbaralhadas, rotulo);
            }

            body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            body.AppendChild(ParagrafoCentralizado("GABARITO", negrito: true, tamanhoMeioPonto: 28));
            body.AppendChild(ParagrafoVazio());
            body.AppendChild(CriarTabelaGabarito(versoes));

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // Gabarito comentado: documento separado com cada questão seguida da resposta
    // e explicação, se houver; usa a ordem original, sem embaralhar.
    public static byte[] GerarGabaritoComentado(ProvaExportDto prova)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.AppendChild(ParagrafoCentralizado(prova.Titulo, negrito: true, tamanhoMeioPonto: 28));
            body.AppendChild(ParagrafoCentralizado("Gabarito comentado", negrito: false, tamanhoMeioPonto: 22));
            body.AppendChild(ParagrafoCentralizado(prova.EscopoRotulo, negrito: false, tamanhoMeioPonto: 20));
            body.AppendChild(ParagrafoVazio());

            var numero = 1;
            foreach (var q in prova.Questoes)
            {
                EscreverEnunciado(mainPart, body, numero, q);

                foreach (var imagem in q.Imagens)
                {
                    EscreverImagemQuestao(mainPart, body, imagem);
                }

                body.AppendChild(ParagrafoNegrito("Resposta correta: " + RespostaResumoCompleto(q)));

                if (q.Tipo == TipoQuestao.Discursiva && !string.IsNullOrWhiteSpace(q.CriterioAvaliacaoDiscursiva))
                {
                    body.AppendChild(Paragrafo("Critério de avaliação: " + q.CriterioAvaliacaoDiscursiva, recuoTwips: 400));
                }

                if (q.ExplicacaoBlocos is { Count: > 0 })
                {
                    body.AppendChild(ParagrafoNegrito("Explicação:"));
                    EscreverBlocosSimples(mainPart, body, q.ExplicacaoBlocos, recuoTwips: 400);
                }
                else if (!string.IsNullOrWhiteSpace(q.Explicacao))
                {
                    body.AppendChild(Paragrafo("Explicação: " + q.Explicacao, recuoTwips: 400));
                }

                body.AppendChild(ParagrafoVazio());
                numero++;
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // Resposta correta por extenso: sem limite de célula, mostra o texto da
    // alternativa (não só a letra) e os pares de associação por extenso.
    private static string RespostaResumoCompleto(QuestaoExportDto q) => q.Tipo switch
    {
        TipoQuestao.MultiplaEscolha => AlternativaCorretaCompleta(q.Alternativas),
        TipoQuestao.CertoErrado => q.RespostaCertoErrado == true ? "Certo" : "Errado",
        TipoQuestao.RespostaBreve => q.RespostaBreveEsperada ?? "—",
        TipoQuestao.Numerica => ResumoNumerica(q.Numerica),
        TipoQuestao.Associacao => ResumoAssociacaoCompleto(q),
        TipoQuestao.Lacunas => ResumoLacunas(q.LacunasRespostas),
        TipoQuestao.Discursiva => q.RespostaEsperadaDiscursiva ?? "—",
        _ => "—",
    };

    private static string AlternativaCorretaCompleta(List<AlternativaExportDto> alternativas)
    {
        var indice = alternativas.FindIndex(a => a.Correta);
        return indice < 0 ? "—" : $"{(char)('A' + indice)}) {alternativas[indice].Texto}";
    }

    // Diferente de ResumoAssociacao: aqui não há coluna B embaralhada, é só o
    // par termo → correspondente na ordem original.
    private static string ResumoAssociacaoCompleto(QuestaoExportDto q) =>
        q.Pares.Count == 0 ? "—" : string.Join("; ", q.Pares.Select((p, i) => $"{i + 1}. {p.Termo} → {p.Correspondente}"));

    // Desenha uma lista de blocos sem o prefixo de numeração nem negrito automático
    // de EscreverEnunciado: usado pra Explicação, texto corrido normal.
    private static void EscreverBlocosSimples(MainDocumentPart mainPart, Body body, List<BlocoMarkdown> blocos, int recuoTwips)
    {
        foreach (var bloco in blocos)
        {
            switch (bloco.Tipo)
            {
                case TipoBlocoMarkdown.Paragrafo:
                    body.AppendChild(ParagrafoComTrechos(mainPart, "", bloco.Trechos, negritoBase: false, recuoTwips: recuoTwips));
                    break;
                case TipoBlocoMarkdown.ItemListaComMarcador:
                    body.AppendChild(ParagrafoComTrechos(mainPart, "•  ", bloco.Trechos, negritoBase: false, recuoTwips: recuoTwips));
                    break;
                case TipoBlocoMarkdown.ItemListaNumerada:
                    body.AppendChild(ParagrafoComTrechos(mainPart, $"{bloco.NumeroLista}.  ", bloco.Trechos, negritoBase: false, recuoTwips: recuoTwips));
                    break;
                case TipoBlocoMarkdown.BlocoCodigo:
                    body.AppendChild(ParagrafoCodigo(bloco.CodigoBruto ?? ""));
                    break;
                case TipoBlocoMarkdown.Tabela:
                    body.AppendChild(CriarTabelaEnunciado(mainPart, bloco));
                    body.AppendChild(ParagrafoVazio());
                    break;

                case TipoBlocoMarkdown.Imagem:
                    if (bloco.Imagem is not null)
                    {
                        EscreverImagemQuestao(mainPart, body, bloco.Imagem);
                    }
                    break;
            }
        }
    }

    // Embaralha ordem das questões e alternativas/coluna B sem afetar as listas
    // originais — cada versão precisa da sua própria cópia independente.
    private static List<QuestaoExportDto> EmbaralharQuestoes(List<QuestaoExportDto> original)
    {
        return original
            .OrderBy(_ => Random.Shared.Next())
            .Select(ClonarComEmbaralhamentoProprio)
            .ToList();
    }

    private static QuestaoExportDto ClonarComEmbaralhamentoProprio(QuestaoExportDto q)
    {
        if (q.Tipo == TipoQuestao.MultiplaEscolha && q.Alternativas.Count > 0)
        {
            return new QuestaoExportDto
            {
                Enunciado = q.Enunciado,
                Tipo = q.Tipo,
                Valor = q.Valor,
                Origem = q.Origem,
                Ano = q.Ano,
                Imagens = q.Imagens,
                EnunciadoBlocos = q.EnunciadoBlocos,
                Alternativas = q.Alternativas.OrderBy(_ => Random.Shared.Next()).ToList(),
            };
        }

        if (q.Tipo == TipoQuestao.Associacao && q.Pares.Count > 0)
        {
            return new QuestaoExportDto
            {
                Enunciado = q.Enunciado,
                Tipo = q.Tipo,
                Valor = q.Valor,
                Origem = q.Origem,
                Ano = q.Ano,
                Imagens = q.Imagens,
                EnunciadoBlocos = q.EnunciadoBlocos,
                Pares = q.Pares,
                OrdemCorrespondentes = Enumerable.Range(0, q.Pares.Count).OrderBy(_ => Random.Shared.Next()).ToList(),
            };
        }

        return q;
    }

    // Escreve cabeçalho + título + instruções + questões de uma versão; reaproveitado
    // pela exportação simples (versionLabel nulo) e por cada versão de GerarVariacoes.
    private static void EscreverVersao(MainDocumentPart mainPart, Body body, ProvaExportDto prova, List<QuestaoExportDto> questoes, string? versionLabel)
    {
        var tituloExibido = versionLabel is null ? prova.Titulo : $"{prova.Titulo} — Versão {versionLabel}";

        if (prova.Instituicao is not null)
        {
            body.AppendChild(CriarTabelaCabecalho(mainPart, prova));
            body.AppendChild(ParagrafoVazio());
            body.AppendChild(ParagrafoCentralizado(tituloExibido, negrito: true, tamanhoMeioPonto: 28));

            if (prova.Instituicao.Instrucoes.Count > 0)
            {
                body.AppendChild(ParagrafoVazio());
                body.AppendChild(CriarQuadroInstrucoes(prova.Instituicao.Instrucoes));
            }
        }
        else
        {
            // Sem instituição vinculada: cabeçalho simples, sem tabela.
            body.AppendChild(ParagrafoCentralizado(tituloExibido, negrito: true, tamanhoMeioPonto: 32));
            body.AppendChild(ParagrafoCentralizado(prova.EscopoRotulo, negrito: false, tamanhoMeioPonto: 22));
            body.AppendChild(ParagrafoVazio());
            body.AppendChild(Paragrafo("Nome: _________________________________________________   Data: ____/____/____"));
            body.AppendChild(Paragrafo("Turma: _______________   Nota: _______________"));
        }

        body.AppendChild(ParagrafoVazio());

        var numero = 1;
        foreach (var q in questoes)
        {
            EscreverEnunciado(mainPart, body, numero, q);

            foreach (var imagem in q.Imagens)
            {
                EscreverImagemQuestao(mainPart, body, imagem);
            }

            switch (q.Tipo)
            {
                case TipoQuestao.MultiplaEscolha:
                    var letra = 'A';
                    foreach (var alternativa in q.Alternativas)
                    {
                        body.AppendChild(Paragrafo($"{letra}) {alternativa.Texto}", recuoTwips: 720));
                        letra++;
                    }
                    break;
                case TipoQuestao.CertoErrado:
                    body.AppendChild(Paragrafo("(   ) Certo        (   ) Errado", recuoTwips: 720));
                    break;
                case TipoQuestao.Discursiva:
                    for (var i = 0; i < 4; i++)
                    {
                        body.AppendChild(Paragrafo("_______________________________________________________________________"));
                    }
                    break;
                case TipoQuestao.RespostaBreve:
                case TipoQuestao.Numerica:
                    body.AppendChild(Paragrafo("Resposta: _______________________________________________", recuoTwips: 720));
                    break;
                case TipoQuestao.Associacao:
                    var ordem = q.OrdemCorrespondentes ?? Enumerable.Range(0, q.Pares.Count).ToList();
                    for (var i = 0; i < q.Pares.Count; i++)
                    {
                        body.AppendChild(Paragrafo($"(    ) {i + 1}. {q.Pares[i].Termo}", recuoTwips: 720));
                    }
                    body.AppendChild(ParagrafoVazio());
                    for (var k = 0; k < ordem.Count; k++)
                    {
                        var letraOpcao = (char)('A' + k);
                        body.AppendChild(Paragrafo($"{letraOpcao}) {q.Pares[ordem[k]].Correspondente}", recuoTwips: 720));
                    }
                    break;
                case TipoQuestao.Lacunas:
                    // Nada extra: as lacunas já vêm numeradas dentro do próprio
                    // Enunciado (ver ProvaExportLoader.SubstituirMarcadoresDeLacuna).
                    break;
            }

            body.AppendChild(ParagrafoVazio());
            numero++;
        }
    }

    // Tabela do gabarito: uma linha por questão, uma coluna por versão da prova,
    // mostrando a letra correta (múltipla escolha), Certo/Errado, ou "—" (dissertativa).
    private static Table CriarTabelaGabarito(List<(string Rotulo, List<QuestaoExportDto> Questoes)> versoes)
    {
        var qtdQuestoes = versoes.Count == 0 ? 0 : versoes[0].Questoes.Count;
        var larguraColunaVersao = (10000 - 1500) / Math.Max(versoes.Count, 1);

        var table = new Table();
        table.AppendChild(CriarBordaCompleta());
        var grid = new TableGrid();
        grid.AppendChild(new GridColumn { Width = "1500" });
        foreach (var _ in versoes)
        {
            grid.AppendChild(new GridColumn { Width = larguraColunaVersao.ToString() });
        }
        table.AppendChild(grid);

        var cabecalho = new TableRow();
        cabecalho.AppendChild(CriarCelulaGabarito("Questão", negrito: true));
        foreach (var v in versoes)
        {
            cabecalho.AppendChild(CriarCelulaGabarito($"Versão {v.Rotulo}", negrito: true));
        }
        table.AppendChild(cabecalho);

        for (var i = 0; i < qtdQuestoes; i++)
        {
            var linha = new TableRow();
            linha.AppendChild(CriarCelulaGabarito((i + 1).ToString(), negrito: false));
            foreach (var v in versoes)
            {
                linha.AppendChild(CriarCelulaGabarito(RespostaResumo(v.Questoes[i]), negrito: false));
            }
            table.AppendChild(linha);
        }

        return table;
    }

    private static TableCell CriarCelulaGabarito(string texto, bool negrito)
    {
        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        cell.AppendChild(ParagrafoCentralizado(texto, negrito, tamanhoMeioPonto: 20));
        return cell;
    }

    private static string RespostaResumo(QuestaoExportDto q) => q.Tipo switch
    {
        TipoQuestao.MultiplaEscolha => LetraCorreta(q.Alternativas),
        TipoQuestao.CertoErrado => q.RespostaCertoErrado == true ? "Certo" : "Errado",
        TipoQuestao.RespostaBreve => q.RespostaBreveEsperada ?? "—",
        TipoQuestao.Numerica => ResumoNumerica(q.Numerica),
        TipoQuestao.Associacao => ResumoAssociacao(q),
        TipoQuestao.Lacunas => ResumoLacunas(q.LacunasRespostas),
        _ => "—",
    };

    private static string LetraCorreta(List<AlternativaExportDto> alternativas)
    {
        var indice = alternativas.FindIndex(a => a.Correta);
        return indice < 0 ? "—" : ((char)('A' + indice)).ToString();
    }

    private static string ResumoNumerica(NumericaExportDto? numerica)
    {
        if (numerica is null)
        {
            return "—";
        }
        return numerica.Tolerancia > 0
            ? $"{numerica.RespostaEsperada:0.####} (±{numerica.Tolerancia:0.####})"
            : $"{numerica.RespostaEsperada:0.####}";
    }

    // Acha em que posição da coluna B embaralhada (OrdemCorrespondentes) o par
    // correto foi impresso; usa sempre a mesma ordem gravada, nunca sorteia de novo.
    private static string ResumoAssociacao(QuestaoExportDto q)
    {
        if (q.Pares.Count == 0)
        {
            return "—";
        }
        var ordem = q.OrdemCorrespondentes ?? Enumerable.Range(0, q.Pares.Count).ToList();
        var pares = Enumerable.Range(0, q.Pares.Count)
            .Select(termoIdx => $"{termoIdx + 1}-{(char)('A' + ordem.IndexOf(termoIdx))}");
        return string.Join(", ", pares);
    }

    private static string ResumoLacunas(List<string> respostas) =>
        respostas.Count == 0 ? "—" : string.Join("; ", respostas.Select((r, i) => $"({i + 1}) {r}"));

    // "1. " normalmente, ou "1. (ENADE 2023) " quando Origem == Enade; sem Ano cai
    // pra "(ENADE)". Mesma lógica duplicada em ProvaPdfExporter.ConstruirPrefixoEnunciado.
    private static string ConstruirPrefixoEnunciado(int numero, QuestaoExportDto q)
    {
        var selo = q.Origem == OrigemQuestao.Enade
            ? q.Ano.HasValue ? $"(ENADE {q.Ano}) " : "(ENADE) "
            : "";
        return $"{numero}. {selo}";
    }

    // Escreve o enunciado (pode virar vários elementos); o prefixo vai sempre no
    // primeiro bloco. O valor da questão, quando ligado, já vem embutido como último trecho.
    private static void EscreverEnunciado(MainDocumentPart mainPart, Body body, int numero, QuestaoExportDto q)
    {
        var prefixo = ConstruirPrefixoEnunciado(numero, q);

        if (q.EnunciadoBlocos is not { Count: > 0 })
        {
            body.AppendChild(ParagrafoNegrito(prefixo + q.Enunciado));
            return;
        }

        var prefixoUsado = false;
        foreach (var bloco in q.EnunciadoBlocos)
        {
            switch (bloco.Tipo)
            {
                case TipoBlocoMarkdown.Paragrafo:
                    body.AppendChild(ParagrafoComTrechos(mainPart, PrefixoOuVazio(ref prefixoUsado, prefixo), bloco.Trechos, negritoBase: true, recuoTwips: 0));
                    break;

                case TipoBlocoMarkdown.ItemListaComMarcador:
                    body.AppendChild(ParagrafoComTrechos(mainPart, PrefixoOuVazio(ref prefixoUsado, prefixo) + "•  ", bloco.Trechos, negritoBase: true, recuoTwips: 400));
                    break;

                case TipoBlocoMarkdown.ItemListaNumerada:
                    body.AppendChild(ParagrafoComTrechos(mainPart, PrefixoOuVazio(ref prefixoUsado, prefixo) + $"{bloco.NumeroLista}.  ", bloco.Trechos, negritoBase: true, recuoTwips: 400));
                    break;

                case TipoBlocoMarkdown.BlocoCodigo:
                    if (!prefixoUsado)
                    {
                        body.AppendChild(ParagrafoNegrito(prefixo));
                        prefixoUsado = true;
                    }
                    body.AppendChild(ParagrafoCodigo(bloco.CodigoBruto ?? ""));
                    break;

                case TipoBlocoMarkdown.Tabela:
                    if (!prefixoUsado)
                    {
                        body.AppendChild(ParagrafoNegrito(prefixo));
                        prefixoUsado = true;
                    }
                    body.AppendChild(CriarTabelaEnunciado(mainPart, bloco));
                    body.AppendChild(ParagrafoVazio());
                    break;

                case TipoBlocoMarkdown.Imagem:
                    if (!prefixoUsado)
                    {
                        body.AppendChild(ParagrafoNegrito(prefixo));
                        prefixoUsado = true;
                    }
                    if (bloco.Imagem is not null)
                    {
                        EscreverImagemQuestao(mainPart, body, bloco.Imagem);
                    }
                    break;
            }
        }
    }

    // Devolve o prefixo ("1. (1 pt) ") só na primeira vez que é chamado — os
    // blocos seguintes do mesmo enunciado não repetem a numeração.
    private static string PrefixoOuVazio(ref bool usado, string prefixo)
    {
        if (usado)
        {
            return "";
        }
        usado = true;
        return prefixo;
    }

    // Monta um parágrafo com trechos, usado pro enunciado e por célula de tabela.
    // negritoBase=true deixa tudo em negrito; false só aplica onde o Markdown pediu.
    private static Paragraph ParagrafoComTrechos(MainDocumentPart mainPart, string prefixoTexto, List<TrechoTexto> trechos, bool negritoBase, int recuoTwips)
    {
        var paragrafo = new Paragraph();

        // Justificado de propósito: texto impresso com as duas margens alinhadas.
        // Blocos de código e tabelas não passam por aqui (têm desenho próprio).
        var paragrafoProps = new ParagraphProperties(new Justification { Val = JustificationValues.Both });
        if (recuoTwips > 0)
        {
            paragrafoProps.AppendChild(new Indentation { Left = recuoTwips.ToString() });
        }
        paragrafo.ParagraphProperties = paragrafoProps;

        if (!string.IsNullOrEmpty(prefixoTexto))
        {
            paragrafo.AppendChild(new Run(new RunProperties(new Bold()), new Text(prefixoTexto) { Space = SpaceProcessingModeValues.Preserve }));
        }

        foreach (var trecho in trechos)
        {
            if (trecho.ImagemPng is { Length: > 0 } png)
            {
                paragrafo.AppendChild(CriarRunImagemInline(mainPart, png, alturaEmu: CmParaEmu(0.5)));
                continue;
            }

            if (string.IsNullOrEmpty(trecho.Texto))
            {
                continue;
            }

            if (trecho.Texto == "\n")
            {
                paragrafo.AppendChild(new Run(new Break()));
                continue;
            }

            var props = new RunProperties();
            if (negritoBase || trecho.Negrito)
            {
                props.AppendChild(new Bold());
            }
            if (trecho.Italico)
            {
                props.AppendChild(new Italic());
            }
            if (trecho.CodigoInline)
            {
                props.AppendChild(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                props.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "EDEDED" });
            }
            paragrafo.AppendChild(new Run(props, new Text(trecho.Texto) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return paragrafo;
    }

    // Bloco de código: uma linha por Run com quebra manual (parágrafo novo traria
    // espaçamento extra), fonte monoespaçada e fundo cinza claro.
    private static Paragraph ParagrafoCodigo(string codigo)
    {
        var paragrafo = new Paragraph(new ParagraphProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = "F4F4F4" },
            new Indentation { Left = "400" }));

        var linhas = codigo.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            if (i > 0)
            {
                paragrafo.AppendChild(new Run(new Break()));
            }
            paragrafo.AppendChild(new Run(
                new RunProperties(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }, new FontSize { Val = "20" }),
                new Text(linhas[i]) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return paragrafo;
    }

    // Tabela dentro do enunciado, mesma borda das outras do documento; primeira
    // linha em negrito quando o Markdown marcou um cabeçalho.
    private static Table CriarTabelaEnunciado(MainDocumentPart mainPart, BlocoMarkdown bloco)
    {
        var linhas = bloco.Tabela ?? new();
        var colunas = linhas.Count > 0 ? linhas[0].Count : 0;

        var table = new Table();
        table.AppendChild(CriarBordaCompleta());
        var grid = new TableGrid();
        for (var c = 0; c < Math.Max(colunas, 1); c++)
        {
            grid.AppendChild(new GridColumn());
        }
        table.AppendChild(grid);

        for (var linhaIdx = 0; linhaIdx < linhas.Count; linhaIdx++)
        {
            var linhaEhCabecalho = bloco.TabelaTemCabecalho && linhaIdx == 0;
            var row = new TableRow();
            foreach (var celula in linhas[linhaIdx])
            {
                var cell = new TableCell();
                cell.AppendChild(new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
                cell.AppendChild(ParagrafoComTrechos(mainPart, "", celula, negritoBase: linhaEhCabecalho, recuoTwips: 0));
                row.AppendChild(cell);
            }
            table.AppendChild(row);
        }

        return table;
    }

    // Igual a CriarParagrafoImagem mas devolve só o Run e dimensiona pela altura,
    // pra casar com a linha de texto ao redor em vez da largura da página.
    private static Run CriarRunImagemInline(MainDocumentPart mainPart, byte[] bytes, int alturaEmu)
    {
        var imagePart = mainPart.AddImagePart("image/png");
        using (var ms = new MemoryStream(bytes))
        {
            imagePart.FeedData(ms);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var dimensoes = ImagemUtils.LerDimensoes(bytes) ?? (Largura: 4, Altura: 1);
        var razao = (double)dimensoes.Largura / dimensoes.Altura;
        var larguraEmu = (int)(alturaEmu * razao);

        var elementoId = (uint)Random.Shared.Next(100000, int.MaxValue);

        var drawing = new Drawing(
            new Dw.Inline(
                new Dw.Extent { Cx = larguraEmu, Cy = alturaEmu },
                new Dw.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new Dw.DocProperties { Id = elementoId, Name = "Formula" },
                new Dw.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new Pic.Picture(
                            new Pic.NonVisualPictureProperties(
                                new Pic.NonVisualDrawingProperties { Id = 0, Name = "Formula" },
                                new Pic.NonVisualPictureDrawingProperties()),
                            new Pic.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new Pic.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = larguraEmu, Cy = alturaEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0,
            });

        return new Run(drawing);
    }

    private static int CmParaEmu(double cm) => (int)(cm * 360000);

    private static Paragraph ParagrafoVazio() => new(new Run(new Text(" ")));

    private static Paragraph Paragrafo(string texto, int recuoTwips = 0)
    {
        var p = new Paragraph();
        if (recuoTwips > 0)
        {
            p.ParagraphProperties = new ParagraphProperties(new Indentation { Left = recuoTwips.ToString() });
        }
        p.AppendChild(new Run(new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));
        return p;
    }

    private static Paragraph ParagrafoNegrito(string texto)
    {
        var run = new Run(new RunProperties(new Bold()), new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static Paragraph ParagrafoCentralizado(string texto, bool negrito, int tamanhoMeioPonto)
    {
        var runProps = new RunProperties(new FontSize { Val = tamanhoMeioPonto.ToString() });
        if (negrito)
        {
            runProps.AppendChild(new Bold());
        }
        var run = new Run(runProps, new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
        var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        p.AppendChild(run);
        return p;
    }

    private static Paragraph ParagrafoNegritoSublinhado(string texto)
    {
        var run = new Run(new RunProperties(new Bold(), new Underline { Val = UnderlineValues.Single }), new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static TableProperties CriarBordaCompleta(string larguraPct = "5000") => new(
        new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 6 },
            new BottomBorder { Val = BorderValues.Single, Size = 6 },
            new LeftBorder { Val = BorderValues.Single, Size = 6 },
            new RightBorder { Val = BorderValues.Single, Size = 6 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }),
        new TableWidth { Type = TableWidthUnitValues.Pct, Width = larguraPct });

    // Tabela do cabeçalho impresso: logo+endereço à esquerda, dados da instituição
    // e campos pra preencher no meio, nota final à direita.
    private static Table CriarTabelaCabecalho(MainDocumentPart mainPart, ProvaExportDto prova)
    {
        var instituicao = prova.Instituicao!;

        var table = new Table();
        table.AppendChild(CriarBordaCompleta());
        table.AppendChild(new TableGrid(
            new GridColumn { Width = "2200" },
            new GridColumn { Width = "5800" },
            new GridColumn { Width = "2200" }));

        var row = new TableRow();

        var leftCell = new TableCell();
        leftCell.AppendChild(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2200" },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        if (instituicao.LogoConteudo is { Length: > 0 } logo)
        {
            leftCell.AppendChild(CriarParagrafoImagem(mainPart, logo, instituicao.LogoContentType ?? "image/png", larguraMaximaEmu: CmParaEmu(2.2), JustificationValues.Center));
        }
        foreach (var linha in new[] { instituicao.Endereco, instituicao.Cidade, instituicao.Telefone, instituicao.Site }.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            leftCell.AppendChild(ParagrafoCentralizado(linha!, negrito: false, tamanhoMeioPonto: 14));
        }
        if (leftCell.ChildElements.Count == 1) // só a TableCellProperties, sem logo/contato
        {
            leftCell.AppendChild(new Paragraph());
        }

        var meioCell = new TableCell();
        meioCell.AppendChild(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "5800" }));
        meioCell.AppendChild(ParagrafoCentralizado(instituicao.Nome, negrito: true, tamanhoMeioPonto: 22));
        meioCell.AppendChild(ParagrafoNegrito($"Professor(a): {prova.Professor ?? Linha(38)}"));
        // No escopo Curso, EscopoRotulo já imprime "Curso: X" sozinho — repetir a
        // linha fixa aqui duplicava o nome do curso no cabeçalho.
        if (prova.TipoEscopo != TipoEscopoProva.Curso)
        {
            meioCell.AppendChild(ParagrafoNegrito($"Curso: {prova.Curso ?? Linha(43)}"));
        }
        meioCell.AppendChild(ParagrafoNegrito(prova.EscopoRotulo));
        // Turma/Data vêm preenchidos quando a prova está amarrada a esses dados;
        // senão, sobra a linha em branco pra preencher à mão.
        var dataTexto = prova.DataAplicacao is { } data ? data.ToString("dd/MM/yyyy") : "____/____/____";
        meioCell.AppendChild(ParagrafoNegrito($"Turma: {prova.Turma ?? Linha(20)}     Data: {dataTexto}"));
        meioCell.AppendChild(ParagrafoNegrito($"Aluno: {Linha(30)}"));

        var notaCell = new TableCell();
        notaCell.AppendChild(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2200" },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        notaCell.AppendChild(ParagrafoCentralizado("NOTA FINAL", negrito: true, tamanhoMeioPonto: 18));
        notaCell.AppendChild(ParagrafoVazio());
        notaCell.AppendChild(ParagrafoVazio());

        row.AppendChild(leftCell);
        row.AppendChild(meioCell);
        row.AppendChild(notaCell);
        table.AppendChild(row);

        return table;
    }

    // Quadro (tabela de uma célula só, com borda) para as instruções da prova.
    private static Table CriarQuadroInstrucoes(List<string> instrucoes)
    {
        var table = new Table();
        table.AppendChild(CriarBordaCompleta());
        table.AppendChild(new TableGrid(new GridColumn { Width = "10000" }));

        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "10000" }));
        cell.AppendChild(ParagrafoNegritoSublinhado("Instruções:"));
        foreach (var instrucao in instrucoes)
        {
            cell.AppendChild(Paragrafo("• " + instrucao, recuoTwips: 200));
        }

        var row = new TableRow();
        row.AppendChild(cell);
        table.AppendChild(row);
        return table;
    }

    private static string Linha(int tamanho) => new('_', tamanho);

    // Largura de referência pra QuestaoImagem.LarguraPercentual e o percentual padrão sem valor próprio.
    private const double LarguraMaximaImagemCm = 16.0;
    private const int LarguraPercentualPadrao = 60;

    // Desenha a imagem seguida da Legenda, se houver, ambas com o mesmo alinhamento.
    private static void EscreverImagemQuestao(MainDocumentPart mainPart, Body body, ImagemExportDto imagem)
    {
        var alinhamento = imagem.Alinhamento ?? AlinhamentoImagem.Centralizado;
        var justificacao = alinhamento switch
        {
            AlinhamentoImagem.Esquerda => JustificationValues.Left,
            AlinhamentoImagem.Direita => JustificationValues.Right,
            _ => JustificationValues.Center,
        };
        var larguraCm = LarguraMaximaImagemCm * (imagem.LarguraPercentual ?? LarguraPercentualPadrao) / 100.0;

        body.AppendChild(CriarParagrafoImagem(mainPart, imagem.Conteudo, imagem.ContentType, larguraMaximaEmu: CmParaEmu(larguraCm), justificacao));

        if (!string.IsNullOrWhiteSpace(imagem.Legenda))
        {
            body.AppendChild(ParagrafoLegendaImagem(imagem.Legenda, justificacao));
        }
    }

    private static Paragraph ParagrafoLegendaImagem(string texto, JustificationValues justificacao)
    {
        var runProps = new RunProperties(new Italic(), new FontSize { Val = "18" });
        var run = new Run(runProps, new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
        var p = new Paragraph(new ParagraphProperties(new Justification { Val = justificacao }));
        p.AppendChild(run);
        return p;
    }

    private static Paragraph CriarParagrafoImagem(MainDocumentPart mainPart, byte[] bytes, string contentType, int larguraMaximaEmu, JustificationValues justificacao)
    {
        // AddImagePart aceita o content-type direto como string nessa versão do SDK
        // (a versão baseada no enum ImagePartType foi descontinuada na v3).
        var imagePart = mainPart.AddImagePart(contentType);
        using (var ms = new MemoryStream(bytes))
        {
            imagePart.FeedData(ms);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var dimensoes = ImagemUtils.LerDimensoes(bytes) ?? (Largura: 4, Altura: 3);
        var razao = (double)dimensoes.Altura / dimensoes.Largura;
        var larguraEmu = larguraMaximaEmu;
        var alturaEmu = (int)(larguraMaximaEmu * razao);

        var elementoId = (uint)Random.Shared.Next(100000, int.MaxValue);

        var drawing = new Drawing(
            new Dw.Inline(
                new Dw.Extent { Cx = larguraEmu, Cy = alturaEmu },
                new Dw.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new Dw.DocProperties { Id = elementoId, Name = "Imagem" },
                new Dw.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new Pic.Picture(
                            new Pic.NonVisualPictureProperties(
                                new Pic.NonVisualDrawingProperties { Id = 0, Name = "Imagem" },
                                new Pic.NonVisualPictureDrawingProperties()),
                            new Pic.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new Pic.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = larguraEmu, Cy = alturaEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0,
            });

        var paragrafo = new Paragraph(new Run(drawing));
        if (justificacao != JustificationValues.Left)
        {
            paragrafo.ParagraphProperties = new ParagraphProperties(new Justification { Val = justificacao });
        }
        return paragrafo;
    }
}
