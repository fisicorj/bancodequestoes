using BancoQuestoes.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace BancoQuestoes.Exportacao;

public static class ProvaPdfExporter
{
    public static byte[] Gerar(ProvaExportDto prova)
    {
        var document = new Document();
        document.Info.Title = prova.Titulo;

        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var section = document.AddSection();
        ConfigurarPagina(section);

        EscreverVersao(section, prova, prova.Questoes, versionLabel: null);

        return Renderizar(document);
    }

    // Gera N versões da mesma prova (Versão A, B, C...), cada uma com a ordem das
    // questões e das alternativas de múltipla escolha embaralhada — dificulta cola
    // entre alunos vizinhos — seguidas de uma página de gabarito com a resposta
    // certa de cada versão. Cada Section do MigraDoc começa em página nova por
    // padrão, então não precisamos inserir quebras de página manualmente.
    public static byte[] GerarVariacoes(ProvaExportDto prova, int quantidadeVersoes)
    {
        var document = new Document();
        document.Info.Title = prova.Titulo;

        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var versoes = new List<(string Rotulo, List<QuestaoExportDto> Questoes)>();

        for (var i = 0; i < quantidadeVersoes; i++)
        {
            var rotulo = ((char)('A' + i)).ToString();
            var questoesEmbaralhadas = EmbaralharQuestoes(prova.Questoes);
            versoes.Add((rotulo, questoesEmbaralhadas));

            var section = document.AddSection();
            ConfigurarPagina(section);
            EscreverVersao(section, prova, questoesEmbaralhadas, rotulo);
        }

        var gabaritoSection = document.AddSection();
        ConfigurarPagina(gabaritoSection);

        var gabTitulo = gabaritoSection.AddParagraph("GABARITO");
        gabTitulo.Format.Font.Size = 16;
        gabTitulo.Format.Font.Bold = true;
        gabTitulo.Format.Alignment = ParagraphAlignment.Center;
        gabTitulo.Format.SpaceAfter = "0.6cm";

        CriarTabelaGabarito(gabaritoSection, versoes);

        return Renderizar(document);
    }

    // "Gabarito comentado": documento separado (NÃO é a prova em branco que o
    // aluno recebe) com cada questão seguida da resposta certa e, se o
    // professor preencheu, da explicação/resolução — pensado como material de
    // estudo/revisão ou apoio na correção. Usa a ordem original das questões
    // (sem embaralhar, diferente de GerarVariacoes) — é um documento do
    // professor, não tem por que variar entre downloads.
    public static byte[] GerarGabaritoComentado(ProvaExportDto prova)
    {
        var document = new Document();
        document.Info.Title = prova.Titulo + " — Gabarito comentado";

        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var section = document.AddSection();
        ConfigurarPagina(section);

        var titulo = section.AddParagraph(prova.Titulo);
        titulo.Format.Font.Size = 16;
        titulo.Format.Font.Bold = true;
        titulo.Format.Alignment = ParagraphAlignment.Center;

        var subtitulo = section.AddParagraph("Gabarito comentado");
        subtitulo.Format.Font.Size = 13;
        subtitulo.Format.Alignment = ParagraphAlignment.Center;

        var disciplina = section.AddParagraph(prova.Disciplina);
        disciplina.Format.Font.Size = 12;
        disciplina.Format.Alignment = ParagraphAlignment.Center;
        disciplina.Format.SpaceAfter = "0.5cm";

        var numero = 1;
        foreach (var q in prova.Questoes)
        {
            EscreverEnunciado(section, numero, "", q);

            foreach (var imagem in q.Imagens)
            {
                AdicionarImagemQuestao(section, imagem);
            }

            var respostaP = section.AddParagraph("Resposta correta: " + RespostaResumoCompleto(q));
            respostaP.Format.Font.Bold = true;
            respostaP.Format.SpaceBefore = "0.2cm";

            if (q.Tipo == TipoQuestao.Discursiva && !string.IsNullOrWhiteSpace(q.CriterioAvaliacaoDiscursiva))
            {
                var criterioP = section.AddParagraph("Critério de avaliação: " + q.CriterioAvaliacaoDiscursiva);
                criterioP.Format.LeftIndent = "0.4cm";
            }

            if (q.ExplicacaoBlocos is { Count: > 0 })
            {
                var explicacaoTitulo = section.AddParagraph("Explicação:");
                explicacaoTitulo.Format.Font.Bold = true;
                EscreverBlocosSimples(section, q.ExplicacaoBlocos, recuoCm: "0.4cm");
            }
            else if (!string.IsNullOrWhiteSpace(q.Explicacao))
            {
                var explicacaoP = section.AddParagraph("Explicação: " + q.Explicacao);
                explicacaoP.Format.LeftIndent = "0.4cm";
            }

            var espaco = section.AddParagraph();
            espaco.Format.SpaceAfter = "0.3cm";
            numero++;
        }

        return Renderizar(document);
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

    // Igual a AdicionarTrechos/AdicionarTabelaEnunciado, mas sem o tratamento
    // de "prefixo de numeração só no primeiro bloco" nem negrito automático de
    // EscreverEnunciado: usado pra Explicação, que é texto corrido normal (só
    // em negrito/itálico onde o Markdown pediu), não a pergunta numerada da prova.
    private static void EscreverBlocosSimples(Section section, List<BlocoMarkdown> blocos, string recuoCm)
    {
        foreach (var bloco in blocos)
        {
            switch (bloco.Tipo)
            {
                case TipoBlocoMarkdown.Paragrafo:
                    var p = section.AddParagraph();
                    p.Format.LeftIndent = recuoCm;
                    AdicionarTrechos(p, bloco.Trechos, negritoBase: false);
                    break;
                case TipoBlocoMarkdown.ItemListaComMarcador:
                case TipoBlocoMarkdown.ItemListaNumerada:
                    var item = section.AddParagraph();
                    item.Format.LeftIndent = recuoCm;
                    item.AddText(bloco.Tipo == TipoBlocoMarkdown.ItemListaNumerada ? $"{bloco.NumeroLista}.  " : "•  ");
                    AdicionarTrechos(item, bloco.Trechos, negritoBase: false);
                    break;
                case TipoBlocoMarkdown.BlocoCodigo:
                    AdicionarBlocoCodigo(section, bloco.CodigoBruto ?? "");
                    break;
                case TipoBlocoMarkdown.Tabela:
                    AdicionarTabelaEnunciado(section, bloco);
                    break;
            }
        }
    }

    private static byte[] Renderizar(Document document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void ConfigurarPagina(Section section)
    {
        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "2cm";
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
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
    // da prova na seção. Reaproveitado tanto pela exportação simples (versionLabel
    // nulo) quanto por cada versão gerada em GerarVariacoes.
    private static void EscreverVersao(Section section, ProvaExportDto prova, List<QuestaoExportDto> questoes, string? versionLabel)
    {
        var tituloExibido = versionLabel is null ? prova.Titulo : $"{prova.Titulo} — Versão {versionLabel}";

        if (prova.Instituicao is not null)
        {
            CriarTabelaCabecalho(section, prova);

            var titulo = section.AddParagraph(tituloExibido);
            titulo.Format.Font.Size = 14;
            titulo.Format.Font.Bold = true;
            titulo.Format.Alignment = ParagraphAlignment.Center;
            titulo.Format.SpaceBefore = "0.4cm";
            titulo.Format.SpaceAfter = "0.4cm";

            if (prova.Instituicao.Instrucoes.Count > 0)
            {
                CriarQuadroInstrucoes(section, prova.Instituicao.Instrucoes);
            }
        }
        else
        {
            // Sem instituição vinculada: cabeçalho simples, sem tabela.
            var titulo = section.AddParagraph(tituloExibido);
            titulo.Format.Font.Size = 16;
            titulo.Format.Font.Bold = true;
            titulo.Format.Alignment = ParagraphAlignment.Center;
            titulo.Format.SpaceBefore = "0.3cm";

            var disciplina = section.AddParagraph($"Disciplina: {prova.Disciplina}");
            disciplina.Format.Alignment = ParagraphAlignment.Center;
            disciplina.Format.SpaceAfter = "0.5cm";

            section.AddParagraph("Nome: _________________________________________________   Data: ____/____/____");
            var turma = section.AddParagraph("Turma: _______________   Nota: _______________");
            turma.Format.SpaceAfter = "0.5cm";
        }

        var numero = 1;
        foreach (var q in questoes)
        {
            var valorTexto = q.Valor is { } v ? $" ({v:0.##} pt{(v == 1 ? "" : "s")})" : "";
            EscreverEnunciado(section, numero, valorTexto, q);

            foreach (var imagem in q.Imagens)
            {
                AdicionarImagemQuestao(section, imagem);
            }

            switch (q.Tipo)
            {
                case TipoQuestao.MultiplaEscolha:
                    var letra = 'A';
                    foreach (var alternativa in q.Alternativas)
                    {
                        var altP = section.AddParagraph($"{letra}) {alternativa.Texto}");
                        altP.Format.LeftIndent = "0.8cm";
                        letra++;
                    }
                    break;
                case TipoQuestao.CertoErrado:
                    var ceP = section.AddParagraph("(   ) Certo        (   ) Errado");
                    ceP.Format.LeftIndent = "0.8cm";
                    break;
                case TipoQuestao.Discursiva:
                    for (var i = 0; i < 4; i++)
                    {
                        section.AddParagraph("_______________________________________________________________________");
                    }
                    break;
                case TipoQuestao.RespostaBreve:
                case TipoQuestao.Numerica:
                    var respP = section.AddParagraph("Resposta: _______________________________________________");
                    respP.Format.LeftIndent = "0.8cm";
                    break;
                case TipoQuestao.Associacao:
                    var ordem = q.OrdemCorrespondentes ?? Enumerable.Range(0, q.Pares.Count).ToList();
                    for (var i = 0; i < q.Pares.Count; i++)
                    {
                        var termoP = section.AddParagraph($"(    ) {i + 1}. {q.Pares[i].Termo}");
                        termoP.Format.LeftIndent = "0.8cm";
                    }
                    var espacoAssoc = section.AddParagraph();
                    espacoAssoc.Format.SpaceAfter = "0.2cm";
                    for (var k = 0; k < ordem.Count; k++)
                    {
                        var letraOpcao = (char)('A' + k);
                        var corrP = section.AddParagraph($"{letraOpcao}) {q.Pares[ordem[k]].Correspondente}");
                        corrP.Format.LeftIndent = "0.8cm";
                    }
                    break;
                case TipoQuestao.Lacunas:
                    // Nada extra: as lacunas já vêm numeradas dentro do próprio
                    // Enunciado (ver ProvaExportLoader.SubstituirMarcadoresDeLacuna).
                    break;
            }

            numero++;
        }
    }

    // Tabela do gabarito: uma linha por questão, uma coluna por versão da prova,
    // mostrando a letra correta (múltipla escolha), Certo/Errado, ou "—" (dissertativa).
    private static void CriarTabelaGabarito(Section section, List<(string Rotulo, List<QuestaoExportDto> Questoes)> versoes)
    {
        var qtdQuestoes = versoes.Count == 0 ? 0 : versoes[0].Questoes.Count;
        var qtdVersoes = Math.Max(versoes.Count, 1);

        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.AddColumn("2.5cm");
        var larguraColuna = $"{13.5 / qtdVersoes:0.##}cm";
        foreach (var _ in versoes)
        {
            table.AddColumn(larguraColuna);
        }

        var cabecalho = table.AddRow();
        AdicionarCelulaGabarito(cabecalho.Cells[0], "Questão", negrito: true);
        for (var i = 0; i < versoes.Count; i++)
        {
            AdicionarCelulaGabarito(cabecalho.Cells[i + 1], $"Versão {versoes[i].Rotulo}", negrito: true);
        }

        for (var i = 0; i < qtdQuestoes; i++)
        {
            var linha = table.AddRow();
            AdicionarCelulaGabarito(linha.Cells[0], (i + 1).ToString(), negrito: false);
            for (var v = 0; v < versoes.Count; v++)
            {
                AdicionarCelulaGabarito(linha.Cells[v + 1], RespostaResumo(versoes[v].Questoes[i]), negrito: false);
            }
        }
    }

    // Escreve o enunciado na seção — pode virar mais de um parágrafo/tabela
    // agora (lista, bloco de código, tabela...), não só um único parágrafo
    // como antes. O prefixo "1. (1 pt)" vai sempre no primeiro bloco.
    private static void EscreverEnunciado(Section section, int numero, string valorTexto, QuestaoExportDto q)
    {
        var prefixo = $"{numero}.{valorTexto} ";

        if (q.EnunciadoBlocos is not { Count: > 0 })
        {
            var p = section.AddParagraph();
            p.Format.Font.Bold = true;
            p.Format.SpaceBefore = "0.4cm";
            p.AddText(prefixo + q.Enunciado);
            return;
        }

        var prefixoUsado = false;
        foreach (var bloco in q.EnunciadoBlocos)
        {
            switch (bloco.Tipo)
            {
                case TipoBlocoMarkdown.Paragrafo:
                    var paragrafo = section.AddParagraph();
                    paragrafo.Format.SpaceBefore = prefixoUsado ? "0.05cm" : "0.4cm";
                    if (!prefixoUsado)
                    {
                        paragrafo.Format.Font.Bold = true;
                        paragrafo.AddText(prefixo);
                        prefixoUsado = true;
                    }
                    AdicionarTrechos(paragrafo, bloco.Trechos, negritoBase: true);
                    break;

                case TipoBlocoMarkdown.ItemListaComMarcador:
                case TipoBlocoMarkdown.ItemListaNumerada:
                    var item = section.AddParagraph();
                    item.Format.LeftIndent = "0.6cm";
                    if (!prefixoUsado)
                    {
                        item.Format.Font.Bold = true;
                        item.Format.SpaceBefore = "0.4cm";
                        item.AddText(prefixo);
                        prefixoUsado = true;
                    }
                    item.AddText(bloco.Tipo == TipoBlocoMarkdown.ItemListaNumerada ? $"{bloco.NumeroLista}.  " : "•  ");
                    AdicionarTrechos(item, bloco.Trechos, negritoBase: true);
                    break;

                case TipoBlocoMarkdown.BlocoCodigo:
                    if (!prefixoUsado)
                    {
                        var linhaPrefixo = section.AddParagraph();
                        linhaPrefixo.Format.Font.Bold = true;
                        linhaPrefixo.Format.SpaceBefore = "0.4cm";
                        linhaPrefixo.AddText(prefixo);
                        prefixoUsado = true;
                    }
                    AdicionarBlocoCodigo(section, bloco.CodigoBruto ?? "");
                    break;

                case TipoBlocoMarkdown.Tabela:
                    if (!prefixoUsado)
                    {
                        var linhaPrefixo = section.AddParagraph();
                        linhaPrefixo.Format.Font.Bold = true;
                        linhaPrefixo.Format.SpaceBefore = "0.4cm";
                        linhaPrefixo.AddText(prefixo);
                        prefixoUsado = true;
                    }
                    AdicionarTabelaEnunciado(section, bloco);
                    break;
            }
        }
    }

    // Preenche um parágrafo já criado com uma lista de trechos (texto normal,
    // negrito/itálico/código herdados do Markdown, imagens de fórmula
    // intercaladas). negritoBase=true deixa tudo em negrito por padrão (mantém
    // a cara de sempre do enunciado); negritoBase=false só aplica onde o
    // Markdown pediu (usado nas células de tabela).
    private static void AdicionarTrechos(Paragraph paragrafo, List<TrechoTexto> trechos, bool negritoBase)
    {
        foreach (var trecho in trechos)
        {
            if (trecho.ImagemPng is { Length: > 0 } png)
            {
                var img = paragrafo.AddImage("base64:" + Convert.ToBase64String(png));
                img.Height = "0.45cm";
                img.LockAspectRatio = true;
                continue;
            }

            if (string.IsNullOrEmpty(trecho.Texto))
            {
                continue;
            }

            if (trecho.Texto == "\n")
            {
                paragrafo.AddLineBreak();
                continue;
            }

            var formatado = paragrafo.AddFormattedText(trecho.Texto);
            if (negritoBase || trecho.Negrito)
            {
                formatado.Font.Bold = true;
            }
            if (trecho.Italico)
            {
                formatado.Font.Italic = true;
            }
            if (trecho.CodigoInline)
            {
                formatado.Font.Name = "Consolas";
                formatado.Font.Bold = false;
            }
        }
    }

    // Bloco de código (```): fundo cinza claro, fonte monoespaçada, uma quebra
    // de linha manual por linha (um parágrafo novo por linha teria espaçamento
    // extra entre elas).
    private static void AdicionarBlocoCodigo(Section section, string codigo)
    {
        var p = section.AddParagraph();
        p.Format.Shading.Color = Color.FromRgb(244, 244, 244);
        p.Format.Font.Name = "Consolas";
        p.Format.Font.Size = 9;
        p.Format.LeftIndent = "0.3cm";
        p.Format.SpaceBefore = "0.15cm";
        p.Format.SpaceAfter = "0.15cm";

        var linhas = codigo.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            if (i > 0)
            {
                p.AddLineBreak();
            }
            p.AddText(linhas[i]);
        }
    }

    // Tabela dentro do enunciado (Markdown |...|...|) — primeira linha em
    // negrito quando o Markdown marcou um cabeçalho.
    private static void AdicionarTabelaEnunciado(Section section, BlocoMarkdown bloco)
    {
        var linhas = bloco.Tabela ?? new();
        if (linhas.Count == 0 || linhas[0].Count == 0)
        {
            return;
        }
        var colunas = linhas[0].Count;

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        var largura = $"{16.0 / colunas:0.##}cm";
        for (var c = 0; c < colunas; c++)
        {
            table.AddColumn(largura);
        }

        for (var linhaIdx = 0; linhaIdx < linhas.Count; linhaIdx++)
        {
            var linhaEhCabecalho = bloco.TabelaTemCabecalho && linhaIdx == 0;
            var row = table.AddRow();
            for (var c = 0; c < linhas[linhaIdx].Count && c < colunas; c++)
            {
                var p = row.Cells[c].AddParagraph();
                AdicionarTrechos(p, linhas[linhaIdx][c], negritoBase: linhaEhCabecalho);
            }
        }
    }

    private static void AdicionarCelulaGabarito(Cell cell, string texto, bool negrito)
    {
        var p = cell.AddParagraph(texto);
        p.Format.Alignment = ParagraphAlignment.Center;
        p.Format.Font.Bold = negrito;
        p.Format.Font.Size = 10;
        cell.VerticalAlignment = VerticalAlignment.Center;
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

    // Mesma lógica do exportador DOCX: a letra do gabarito de cada termo é a
    // posição em que seu par correto foi impresso na coluna B embaralhada
    // (OrdemCorrespondentes), nunca recalculada com um novo sorteio aqui.
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

    // Tabela com borda no molde de um cabeçalho de prova impresso: logo +
    // endereço à esquerda; nome da instituição e campos pra preencher (Professor,
    // Curso, Disciplina, Período, Aluno) no meio; quadro de nota final à direita.
    private static void CriarTabelaCabecalho(Section section, ProvaExportDto prova)
    {
        var instituicao = prova.Instituicao!;

        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.AddColumn("3cm");
        table.AddColumn("10.5cm");
        table.AddColumn("2.5cm");

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;

        var leftCell = row.Cells[0];
        if (instituicao.LogoConteudo is { Length: > 0 } logo)
        {
            // O prefixo "base64:" é o jeito documentado do MigraDoc de aceitar uma
            // imagem que veio da memória (banco), sem precisar salvar em disco antes.
            var imagemLogo = leftCell.AddImage("base64:" + Convert.ToBase64String(logo));
            imagemLogo.Width = "2.3cm";
            imagemLogo.LockAspectRatio = true;
        }

        foreach (var linha in new[] { instituicao.Endereco, instituicao.Cidade, instituicao.Telefone, instituicao.Site }.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var contatoP = leftCell.AddParagraph(linha!);
            contatoP.Format.Font.Size = 8;
            contatoP.Format.Alignment = ParagraphAlignment.Center;
        }

        var meioCell = row.Cells[1];
        var nomeInst = meioCell.AddParagraph(instituicao.Nome);
        nomeInst.Format.Font.Size = 12;
        nomeInst.Format.Font.Bold = true;
        nomeInst.Format.Alignment = ParagraphAlignment.Center;
        nomeInst.Format.SpaceAfter = "0.15cm";

        AdicionarCampo(meioCell, $"Professor(a): {prova.Professor ?? Linha(38)}");
        AdicionarCampo(meioCell, $"Curso: {prova.Curso ?? Linha(43)}");
        AdicionarCampo(meioCell, $"Disciplina: {prova.Disciplina}");
        var dataTexto = prova.DataAplicacao is { } data ? data.ToString("dd/MM/yyyy") : "____/____/____";
        AdicionarCampo(meioCell, $"Turma: {prova.Turma ?? Linha(20)}     Data: {dataTexto}");
        AdicionarCampo(meioCell, $"Aluno: {Linha(30)}");

        var notaCell = row.Cells[2];
        var notaTitulo = notaCell.AddParagraph("NOTA FINAL");
        notaTitulo.Format.Font.Bold = true;
        notaTitulo.Format.Font.Size = 9;
        notaTitulo.Format.Alignment = ParagraphAlignment.Center;
        var espacoNota = notaCell.AddParagraph();
        espacoNota.Format.SpaceAfter = "1cm";
    }

    // Quadro (tabela de uma célula só, com borda) para as instruções da prova.
    private static void CriarQuadroInstrucoes(Section section, List<string> instrucoes)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.AddColumn("16cm");

        var cell = table.AddRow().Cells[0];
        var titulo = cell.AddParagraph("Instruções:");
        titulo.Format.Font.Bold = true;
        titulo.Format.Font.Underline = Underline.Single;

        foreach (var instrucao in instrucoes)
        {
            var linha = cell.AddParagraph("•  " + instrucao);
            linha.Format.LeftIndent = "0.5cm";
            linha.Format.Font.Size = 10;
        }
    }

    private static void AdicionarCampo(Cell cell, string texto)
    {
        var p = cell.AddParagraph(texto);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 10;
    }

    private static string Linha(int tamanho) => new('_', tamanho);

    // Largura "cheia" de referência pra imagem de questão (percentuais em
    // QuestaoImagem.LarguraPercentual são relativos a essa medida) e o
    // percentual usado quando a imagem não tem um valor próprio definido —
    // mesmos valores usados no exportador DOCX, pra manter a cara parecida
    // entre os dois formatos.
    private const double LarguraMaximaImagemCm = 16.0;
    private const int LarguraPercentualPadrao = 60;

    // Desenha a imagem de uma questão (dentro de um parágrafo, pra herdar o
    // alinhamento) seguida da Legenda, se houver — os dois alinhados juntos,
    // do jeito que aparecem impressos na prova.
    private static void AdicionarImagemQuestao(Section section, ImagemExportDto imagem)
    {
        var alinhamento = ConverterAlinhamento(imagem.Alinhamento ?? AlinhamentoImagem.Centralizado);
        var larguraCm = LarguraMaximaImagemCm * (imagem.LarguraPercentual ?? LarguraPercentualPadrao) / 100.0;

        var p = section.AddParagraph();
        p.Format.Alignment = alinhamento;
        var img = p.AddImage("base64:" + Convert.ToBase64String(imagem.Conteudo));
        img.Width = $"{larguraCm:0.##}cm";
        img.LockAspectRatio = true;

        if (!string.IsNullOrWhiteSpace(imagem.Legenda))
        {
            var legenda = section.AddParagraph(imagem.Legenda);
            legenda.Format.Alignment = alinhamento;
            legenda.Format.Font.Italic = true;
            legenda.Format.Font.Size = 9;
            legenda.Format.SpaceAfter = "0.3cm";
        }
    }

    private static ParagraphAlignment ConverterAlinhamento(AlinhamentoImagem alinhamento) => alinhamento switch
    {
        AlinhamentoImagem.Esquerda => ParagraphAlignment.Left,
        AlinhamentoImagem.Direita => ParagraphAlignment.Right,
        _ => ParagraphAlignment.Center,
    };
}
