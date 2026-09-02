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

    // Gera N versões da mesma prova (Versão A, B, C...), cada uma com a ordem das
    // questões e das alternativas de múltipla escolha embaralhada — dificulta cola
    // entre alunos vizinhos — seguidas de uma página de gabarito com a resposta
    // certa de cada versão.
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

    // "Gabarito comentado": documento separado (NÃO é a prova em branco que o
    // aluno recebe) com cada questão seguida da resposta certa e, se o
    // professor preencheu, da explicação/resolução — pensado como material de
    // estudo/revisão ou apoio na correção. Usa a ordem original das questões
    // (sem embaralhar, diferente de GerarVariacoes) — é um documento do
    // professor, não tem por que variar entre downloads.
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
            body.AppendChild(ParagrafoCentralizado(prova.Disciplina, negrito: false, tamanhoMeioPonto: 20));
            body.AppendChild(ParagrafoVazio());

            var numero = 1;
            foreach (var q in prova.Questoes)
            {
                EscreverEnunciado(mainPart, body, numero, "", q);

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

    // Resposta correta "por extenso" — versão mais completa da RespostaResumo
    // usada na tabela compacta de gabarito por versão: aqui não há limite de
    // largura de célula, então mostra o texto da alternativa (não só a letra),
    // os pares de associação por extenso (não a notação "1-A, 2-C") etc.
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

    // Diferente de ResumoAssociacao (que devolve "1-A, 2-C..." pra caber numa
    // célula da tabela de gabarito por versão): aqui não existe coluna B
    // embaralhada pra referenciar — é só o par termo → correspondente na
    // ordem original, um por linha, igual o professor cadastrou.
    private static string ResumoAssociacaoCompleto(QuestaoExportDto q) =>
        q.Pares.Count == 0 ? "—" : string.Join("; ", q.Pares.Select((p, i) => $"{i + 1}. {p.Termo} → {p.Correspondente}"));

    // Desenha uma lista de blocos (parágrafo, item de lista, código, tabela —
    // ver BlocoMarkdown) sem o tratamento de "prefixo de numeração só no
    // primeiro bloco" nem negrito automático de EscreverEnunciado: usado pra
    // Explicação, que é texto corrido normal (só em negrito/itálico onde o
    // Markdown pediu), não a pergunta numerada da prova.
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
            }
        }
    }

    // Embaralha a ordem das questões e, dentro de cada múltipla escolha ou
    // associação, a ordem das alternativas/coluna B — sem afetar as listas
    // originais (cada versão precisa da sua própria cópia independente, senão
    // embaralhar uma bagunçaria as outras).
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
                Imagens = q.Imagens,
                EnunciadoBlocos = q.EnunciadoBlocos,
                Pares = q.Pares,
                OrdemCorrespondentes = Enumerable.Range(0, q.Pares.Count).OrderBy(_ => Random.Shared.Next()).ToList(),
            };
        }

        return q;
    }

    // Escreve o cabeçalho + título + instruções + lista de questões de UMA versão
    // da prova no corpo do documento. Reaproveitado tanto pela exportação simples
    // (versionLabel nulo) quanto por cada versão gerada em GerarVariacoes.
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
            body.AppendChild(ParagrafoCentralizado($"Disciplina: {prova.Disciplina}", negrito: false, tamanhoMeioPonto: 22));
            body.AppendChild(ParagrafoVazio());
            body.AppendChild(Paragrafo("Nome: _________________________________________________   Data: ____/____/____"));
            body.AppendChild(Paragrafo("Turma: _______________   Nota: _______________"));
        }

        body.AppendChild(ParagrafoVazio());

        var numero = 1;
        foreach (var q in questoes)
        {
            var valorTexto = q.Valor is { } v ? $" ({v:0.##} pt{(v == 1 ? "" : "s")})" : "";
            EscreverEnunciado(mainPart, body, numero, valorTexto, q);

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

    // Para cada termo (na ordem original), acha em que posição da coluna B
    // embaralhada (OrdemCorrespondentes) o seu par correto foi impresso —
    // essa posição É a letra usada no gabarito, então tem que usar a MESMA
    // ordem gravada no DTO, nunca sortear de novo aqui.
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

    // Escreve o enunciado direto no corpo do documento — pode virar mais de um
    // elemento agora (parágrafo + lista + bloco de código + tabela, por
    // exemplo), não só um Paragraph como antes. O prefixo "1. (1 pt)" vai
    // sempre no primeiro bloco, seja ele qual for.
    private static void EscreverEnunciado(MainDocumentPart mainPart, Body body, int numero, string valorTexto, QuestaoExportDto q)
    {
        var prefixo = $"{numero}.{valorTexto} ";

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

    // Monta um parágrafo a partir de uma lista de trechos (texto normal,
    // negrito/itálico/código herdados do Markdown, e imagens de fórmula
    // intercaladas) — usado tanto pro enunciado quanto pra cada célula de
    // tabela. negritoBase=true aplica negrito a TUDO por padrão (mantém o
    // enunciado com a mesma cara "em negrito" de sempre, mesmo sem Markdown);
    // negritoBase=false só aplica onde o Markdown pediu explicitamente
    // (usado em células de tabela, que não deveriam virar uma parede de negrito).
    private static Paragraph ParagrafoComTrechos(MainDocumentPart mainPart, string prefixoTexto, List<TrechoTexto> trechos, bool negritoBase, int recuoTwips)
    {
        var paragrafo = new Paragraph();
        if (recuoTwips > 0)
        {
            paragrafo.ParagraphProperties = new ParagraphProperties(new Indentation { Left = recuoTwips.ToString() });
        }

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

    // Bloco de código (```): uma linha por Run separado por quebra manual (não
    // por parágrafo — parágrafos novos trariam espaçamento extra entre linhas),
    // fonte monoespaçada e fundo cinza claro pra destacar do resto do enunciado.
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

    // Tabela dentro do enunciado (Markdown |...|...|) — mesma borda das outras
    // tabelas do documento; primeira linha em negrito quando o Markdown marcou
    // um cabeçalho (linha separadora "---" logo abaixo dela).
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

    // Igual a CriarParagrafoImagem, mas devolve só o Run (não um Paragraph novo)
    // e dimensiona pela ALTURA (não largura) — o objetivo aqui é casar com a
    // altura da linha de texto ao redor, não ocupar a largura da página.
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

    // Tabela com borda no molde de um cabeçalho de prova impresso: logo +
    // endereço à esquerda; nome da instituição e campos pra preencher (Professor,
    // Curso, Disciplina, Período, Aluno) no meio; quadro de nota final à direita.
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
        meioCell.AppendChild(ParagrafoNegrito($"Curso: {prova.Curso ?? Linha(43)}"));
        meioCell.AppendChild(ParagrafoNegrito($"Disciplina: {prova.Disciplina}"));
        // Turma/Data vêm preenchidos quando a prova está amarrada a esses dados
        // (ver ProvaForm.razor); senão, sobra a linha em branco de sempre pra
        // preencher à mão na hora da aplicação impressa.
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

    // Largura "cheia" de referência pra imagem de questão (percentuais em
    // QuestaoImagem.LarguraPercentual são relativos a essa medida) e o
    // percentual usado quando a imagem não tem um valor próprio definido.
    private const double LarguraMaximaImagemCm = 16.0;
    private const int LarguraPercentualPadrao = 60;

    // Desenha a imagem de uma questão (parágrafo com a figura, alinhada
    // conforme QuestaoImagem.Alinhamento) seguida da Legenda, se houver —
    // ambos alinhados juntos, do jeito que aparecem impressos na prova.
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
