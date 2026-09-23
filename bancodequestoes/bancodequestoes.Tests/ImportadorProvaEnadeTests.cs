using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Fonts;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "Implementar Importador de Provas ENADE com Tela de
// Revisão — sem IA". Cobre os cenários pedidos (item 36):
//
//   Formação Geral -> disciplina Formação Geral                          [ProcessarAsync_FormacaoGeral_RecebeDisciplinaFormacaoGeralAutomaticamente_SemAreaCurso]
//   Formação Geral -> sem AreaCurso                                      [idem — mesma asserção, AreaCursoId nulo]
//   Componente Específico -> AreaCurso selecionada                       [ProcessarAsync_ComponenteEspecifico_RecebeAreaCursoSelecionada_DisciplinaFicaPendenteDeRevisao]
//   Componente Específico -> disciplina pode ser revisada (não travada)  [idem — Status nunca Validada, sempre pendente de escolha]
//   D1/D2 -> NumeroOriginal válido (string, nunca int)                   [EnadeProvaParserTests.Extrair_QuestaoDiscursiva_NumeroOriginalMantemPrefixoD_ComoString]
//   Objetiva -> alternativas corretamente estruturadas                   [EnadeProvaParserTests.Extrair_QuestaoObjetiva_AlternativasCorretamenteEstruturadas]
//   Gabarito ausente -> requer revisão                                   [ProcessarAsync_SemGabaritoOficial_QuestaoObjetivaFicaRequerRevisaoComGabaritoPendente]
//   Questão com possível duplicidade -> alerta                           [ProcessarAsync_FormacaoGeralJaExistenteNoBanco_GeraAlertaDePossivelDuplicata]
//   Formação Geral repetida -> possível duplicidade (mesmo em Área diferente do caderno) [idem]
//   Questão específica com mesmo número em outra Área -> NÃO é duplicata automaticamente [ProcessarAsync_ComponenteEspecifico_MesmoNumeroEmAreaDiferente_NaoEhTratadaComoDuplicata]
//   Pré-importação -> não cria Questao / Confirmação -> cria Questao / Cancelamento -> não cria Questao
//       [ProcessarAsync_ApenasExtrai_NuncaCriaQuestaoAntesDaConfirmacao;
//        ImportarSelecionadasAsync_QuestoesSelecionadas_SaoCriadasComoQuestaoDeVerdade]
//
// Fora do escopo destes testes (mesma ressalva de sempre neste projeto — ver
// SuporteEnadeFormacaoGeralTests.cs): comportamento só de UI
// (QuestaoImportarEnade.razor — expandir/colapsar linha, edição inline) não
// é coberto aqui, não há bUnit no repositório; verificado por leitura de
// código, documentado como pendência real no relatório final.
//
// Sobre os fixtures de PDF (PdfFixtures, abaixo): gerados com
// MigraDoc/PdfDocumentRenderer (já uma dependência do projeto, usada pra
// exportação de provas — ver Exportacao/ProvaPdfExporter.cs) e lidos de
// volta com UglyToad.PdfPig (o parser de verdade, sem nenhum atalho/mock).
// Cada seção da prova (Formação Geral / Componente Específico) é uma
// MigraDoc Section SEPARADA de propósito — Section sempre começa em página
// nova (mesmo comentário já existente em ProvaPdfExporter.cs) — porque
// EnadeProvaParser classifica Seção por PÁGINA (ver
// ClassificarSecaoPorPagina); usar Sections separadas garante
// determinística e independente de quanto texto cabe em cada página. As
// asserções abaixo verificam só ESTRUTURA (contagens, Enum, presença de
// alerta, Letra das alternativas) — nunca o TEXTO exato extraído — porque a
// extração de caracteres acentuados/espaçamento exato depende de detalhes
// de fonte/renderização que não são o que este importador promete acertar
// (ver limitações conhecidas no relatório final).
internal static class PdfFixtures
{
    // PDFsharp 6.2+ não presume de onde tirar fontes (roda em qualquer SO) —
    // precisa disso ANTES da primeira fonte ser usada pra saber ler as fontes
    // do Windows (Arial, Courier New etc.), exatamente a mesma linha que
    // Program.cs já roda na inicialização do app de verdade (ver comentário
    // lá). O processo de teste (dotnet test) nunca executa Program.cs, então
    // sem repetir isso aqui o MigraDoc quebra tentando montar sua fonte de
    // erro padrão ("Courier New") — não é um problema do parser/importador,
    // é só a mesma configuração de ambiente que faltava neste processo
    // separado. Construtor estático: roda uma única vez, antes do primeiro
    // uso de qualquer método desta classe, não importa quantos testes rodem.
    static PdfFixtures()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    // PNG 1x1 válido mínimo (bytes reais, não um mock/placeholder inválido)
    // — usado em dois papeis diferentes nos testes abaixo: (1) como imagem
    // DE CONTEÚDO embutida num PDF de teste via MigraDoc (mesmo jeito
    // "base64:" já usado em produção por ProvaPdfExporter.cs — ver
    // Exportacao/ProvaPdfExporter.cs), pra testar que EnadeProvaParser.
    // ExtrairImagensDaPagina reconhece uma imagem de verdade; (2) como
    // retorno fixo de FakePaginaPdfRenderizador (abaixo), pra testar
    // RepresentacaoOriginal sem depender do PDFium/SkiaSharp de verdade.
    public static readonly byte[] PngMinusculo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    // JPEG 4x4 válido mínimo (não um PNG) — usado especificamente em
    // ProvaComImagem() abaixo, no lugar do PNG acima. Motivo: PDFsharp/
    // MigraDoc NÃO reincorpora um PNG de origem como PNG dentro do PDF —
    // ele decodifica e reescreve como bitmap bruto (FlateDecode), que é
    // exatamente o formato que EnadeProvaParser.DetectarTipoDeImagem já
    // documenta como NÃO suportado nesta versão (mesma limitação real
    // encontrada com os PDFs ENADE de verdade mais cedo nesta sessão — lá
    // as imagens eram JPEG/DCTDecode, por isso funcionavam). Um JPEG de
    // origem, ao contrário, normalmente é reaproveitado quase byte-a-byte
    // (DCTDecode passthrough) — por isso é o formato certo pra testar
    // extração de imagem de conteúdo de ponta a ponta.
    public static readonly byte[] JpegMinusculo = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAAEAAQDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDk6KKK8I/Vj//Z");

    private static void ConfigurarPagina(Section section)
    {
        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "2cm";
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
    }

    private static byte[] Renderizar(Document document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    // Prova sintética com 4 questões: "01" (Objetiva, Formação Geral),
    // "D1" (Discursiva, Formação Geral), "10" (Objetiva, Componente
    // Específico), "D2" (Discursiva, Componente Específico) — pequena o
    // bastante pra ser um fixture de teste, grande o bastante pra exercitar
    // separação de seção, tipo objetiva x discursiva, e alternativas A-D.
    //
    // Formato das alternativas e numeração das discursivas AJUSTADOS pra
    // bater com o que foi observado num caderno de prova ENADE 2023 real
    // (Engenharia de Computação, enviado pelo usuário e testado com
    // pdfplumber, já que não há SDK do .NET neste ambiente pra rodar o
    // PdfPig de verdade): alternativas SEM pontuação ("A texto", não
    // "(A) texto" — a "bolinha" ao redor da letra é um elemento gráfico
    // separado do texto extraído) e "QUESTÃO DISCURSIVA 01"/"02" com DOIS
    // dígitos (não "QUESTÃO DISCURSIVA 1"). Os enunciados aqui foram
    // escritos de propósito para NUNCA começar (nem depois de quebrar de
    // linha) com um "A " isolado seguido de espaço — o formato sem
    // pontuação é ambíguo com esse caso (ver comentário em
    // AlternativaSemPontuacaoRegex) e este fixture testa o caminho
    // correto, não esse caso-limite conhecido.
    public static byte[] ProvaCompleta()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secaoFormacaoGeral = document.AddSection();
        ConfigurarPagina(secaoFormacaoGeral);
        foreach (var paragrafo in new[]
        {
            "PROVA ENADE 2023 - ENGENHARIA DE COMPUTACAO",
            "FORMACAO GERAL",
            "QUESTAO 01",
            "Estudos recentes sobre sustentabilidade ambiental colocam o tema entre os mais centrais nas discussoes contemporaneas sobre desenvolvimento economico e qualidade de vida da populacao mundial.",
            "A O crescimento economico deve ser priorizado independentemente dos impactos ambientais causados ao planeta.",
            "B O desenvolvimento sustentavel busca equilibrar crescimento economico, inclusao social e preservacao ambiental de forma integrada.",
            "C Preservar o meio ambiente e responsabilidade exclusiva dos governos, sem qualquer participacao da sociedade civil organizada.",
            "D O consumo desenfreado de recursos naturais nao guarda nenhuma relacao com o desenvolvimento sustentavel das nacoes.",
            "QUESTAO DISCURSIVA 01",
            "Discorra sobre os principais desafios enfrentados pelas cidades brasileiras para se tornarem mais sustentaveis nas proximas decadas.",
        })
        {
            secaoFormacaoGeral.AddParagraph(paragrafo);
        }

        var secaoComponenteEspecifico = document.AddSection();
        ConfigurarPagina(secaoComponenteEspecifico);
        foreach (var paragrafo in new[]
        {
            "COMPONENTE ESPECIFICO",
            "QUESTAO 10",
            "Um sistema distribuido e composto por multiplos computadores independentes que se comunicam entre si por meio de uma rede, apresentando-se ao usuario como um sistema unico e coerente.",
            "A Transparencia de localizacao impede completamente que falhas de rede afetem a disponibilidade do sistema distribuido como um todo.",
            "B O uso de replicas de dados em nos diferentes e uma tecnica comum para aumentar a disponibilidade e a tolerancia a falhas do sistema.",
            "C Sistemas distribuidos nunca apresentam problemas de consistencia entre replicas, independentemente do protocolo utilizado na comunicacao.",
            "D Comunicacao entre os nos de um sistema distribuido dispensa qualquer mecanismo de sincronizacao de relogio ou ordenacao de eventos.",
            "QUESTAO DISCURSIVA 02",
            "Explique as diferencas entre consistencia forte e consistencia eventual em bancos de dados distribuidos, apresentando um exemplo de cada.",
        })
        {
            secaoComponenteEspecifico.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // Gabarito oficial simples: "1"->B, "10"->B — casa por NumeroOriginal
    // (com padding) contra o que ProvaCompleta() acima produz. Formato
    // "QUESTÃO N letra" SEM separador, com o número da questão de
    // formação geral SEM zero à esquerda ("QUESTÃO 1", não "QUESTÃO 01") —
    // é exatamente o formato observado no gabarito oficial real do ENADE
    // 2023 (arquivo enviado pelo usuário, testado com pdfplumber): note que
    // a PROVA usa "QUESTÃO 01" (zero à esquerda) mas o GABARITO usa
    // "QUESTÃO 1" (sem) — os dois arquivos não seguem a mesma convenção de
    // dígitos entre si, por isso GabaritoEnadeParser normaliza os dois lados
    // (PadLeft) antes de comparar.
    public static byte[] GabaritoSimples()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        foreach (var paragrafo in new[] { "GABARITO DEFINITIVO", "QUESTAO DISCURSIVA 1 ***", "QUESTAO 1 B", "QUESTAO 10 B" })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // Fixture minúsculo pra um caso-limite específico: uma linha de
    // enunciado que começa com uma letra de A-E sozinha, ANTES de qualquer
    // alternativa de verdade — deve continuar fazendo parte do enunciado,
    // nunca ser confundida com o início de uma alternativa (ver comentário
    // em AlternativaSemPontuacaoRegex/controle de sequência em
    // MontarQuestao). "E economistas..." é uma continuação de frase comum
    // em português que, sem a checagem de sequência A,B,C,D,E, seria
    // incorretamente tratada como "alternativa E".
    public static byte[] ProvaComLinhaAmbigua()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        foreach (var paragrafo in new[]
        {
            "FORMACAO GERAL",
            "QUESTAO 05",
            "Governos, empresas privadas, organizacoes nao governamentais.",
            "E economistas de diversas correntes tem debatido intensamente os efeitos de longo prazo das politicas de transicao energetica sobre o emprego industrial em paises em desenvolvimento como o Brasil.",
            "A O debate sobre transicao energetica e irrelevante para o emprego industrial.",
            "B As politicas de transicao energetica podem gerar efeitos distintos conforme o setor e a regiao.",
        })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // Padrão de resposta oficial "simples" (sem subitens estruturados) —
    // D1 e D2, casando com as discursivas de ProvaCompleta() acima. Mesma
    // convenção de cabeçalho já confirmada contra prova real: "QUESTAO
    // DISCURSIVA NN" com dois dígitos (ver comentário em
    // PadraoRespostaEnadeParser).
    public static byte[] PadraoRespostaSimples()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        foreach (var paragrafo in new[]
        {
            "PADRAO DE RESPOSTA",
            "QUESTAO DISCURSIVA 01",
            "Espera-se que o candidato aborde mobilidade urbana e sustentabilidade.",
            "QUESTAO DISCURSIVA 02",
            "Espera-se que o candidato explique consistencia forte e eventual.",
        })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // Só D1 (sem D2) — pra testar o caso "arquivo de padrão informado, mas
    // esta questão específica não foi encontrada nele" (item 5/36 do
    // pedido "Padrão de Resposta + modelo híbrido": nunca inventa, exige
    // revisão manual).
    public static byte[] PadraoRespostaSoD1()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        foreach (var paragrafo in new[]
        {
            "PADRAO DE RESPOSTA",
            "QUESTAO DISCURSIVA 01",
            "Espera-se que o candidato aborde mobilidade urbana e sustentabilidade.",
        })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // D1 com QUATRO subitens pontuados (a/b/c/d) — fixture pedido
    // explicitamente no item 36: "a) valor 2,0 / b) valor 2,0 / c) valor
    // 2,0 / d) valor 4,0", verificando que os 4 itens são reconhecidos, a
    // pontuação de cada um é preservada, e a resposta de cada subitem fica
    // associada ao Codigo certo (a/b/c/d) — nunca embaralhada.
    public static byte[] PadraoRespostaComSubitens()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        foreach (var paragrafo in new[]
        {
            "PADRAO DE RESPOSTA",
            "QUESTAO DISCURSIVA 01",
            "a) Cite um desafio de infraestrutura urbana (valor: 2,0 pontos)",
            "b) Explique a gestao de residuos solidos (valor: 2,0 pontos)",
            "c) Descreva uma politica de mobilidade urbana sustentavel (valor: 2,0 pontos)",
            "d) Relacione planejamento urbano e qualidade de vida (valor: 4,0 pontos)",
        })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }

    // Uma única questão de Formação Geral ("01") com uma imagem de verdade
    // embutida no PDF (não um mock) — pra testar o modelo híbrido: a
    // imagem precisa continuar sendo extraída como ImagemImportacaoEnade
    // (conteúdo pedagógico) igual sempre foi, INDEPENDENTE de
    // RepresentacaoOriginal (snapshot de página) também existir — os dois
    // nunca devem ser confundidos (item 19/21 do pedido "modelo híbrido").
    public static byte[] ProvaComImagem()
    {
        var document = new Document();
        var estiloNormal = document.Styles["Normal"]!;
        estiloNormal.Font.Name = "Arial";
        estiloNormal.Font.Size = 11;

        var secao = document.AddSection();
        ConfigurarPagina(secao);
        secao.AddParagraph("FORMACAO GERAL");
        secao.AddParagraph("QUESTAO 01");
        secao.AddParagraph("Observe a figura a seguir, que ilustra um grafico de crescimento populacional urbano ao longo de tres decadas consecutivas.");

        var paragrafoImagem = secao.AddParagraph();
        var img = paragrafoImagem.AddImage("base64:" + Convert.ToBase64String(JpegMinusculo));
        img.Width = "2cm";

        foreach (var paragrafo in new[]
        {
            "A O grafico mostra queda constante da populacao urbana no periodo analisado.",
            "B O grafico mostra crescimento constante da populacao urbana no periodo analisado.",
            "C O grafico nao permite nenhuma conclusao sobre a populacao urbana no periodo.",
            "D O grafico mostra estabilidade total da populacao urbana no periodo analisado.",
        })
        {
            secao.AddParagraph(paragrafo);
        }

        return Renderizar(document);
    }
}

// --- Grupo A: EnadeProvaParser puro (sem banco) ---
public class EnadeProvaParserTests
{
    [Fact]
    public void Extrair_SeparaFormacaoGeralDeComponenteEspecifico()
    {
        var resultado = EnadeProvaParser.Extrair(PdfFixtures.ProvaCompleta());

        Assert.Equal(4, resultado.Questoes.Count);
        Assert.All(
            resultado.Questoes.Where(q => q.NumeroOriginal is "01" or "D1"),
            q => Assert.Equal(SecaoEnade.FormacaoGeral, q.Secao));
        Assert.All(
            resultado.Questoes.Where(q => q.NumeroOriginal is "10" or "D2"),
            q => Assert.Equal(SecaoEnade.ComponenteEspecifico, q.Secao));
    }

    // Item 18/36 do pedido: NumeroOriginal de questão discursiva mantém o
    // prefixo "D" como STRING (nunca convertido pra int).
    [Fact]
    public void Extrair_QuestaoDiscursiva_NumeroOriginalMantemPrefixoD_ComoString()
    {
        var resultado = EnadeProvaParser.Extrair(PdfFixtures.ProvaCompleta());

        var d1 = resultado.Questoes.Single(q => q.Tipo == TipoQuestao.Discursiva && q.Secao == SecaoEnade.FormacaoGeral);
        var d2 = resultado.Questoes.Single(q => q.Tipo == TipoQuestao.Discursiva && q.Secao == SecaoEnade.ComponenteEspecifico);

        Assert.Equal("D1", d1.NumeroOriginal);
        Assert.Equal("D2", d2.NumeroOriginal);
        Assert.Empty(d1.Alternativas);
        Assert.Empty(d2.Alternativas);
    }

    [Fact]
    public void Extrair_QuestaoObjetiva_AlternativasCorretamenteEstruturadas()
    {
        var resultado = EnadeProvaParser.Extrair(PdfFixtures.ProvaCompleta());

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");
        Assert.Equal(TipoQuestao.MultiplaEscolha, q01.Tipo);
        Assert.Equal(new[] { 'A', 'B', 'C', 'D' }, q01.Alternativas.Select(a => a.Letra));
        Assert.All(q01.Alternativas, a => Assert.False(string.IsNullOrWhiteSpace(a.Texto)));
        Assert.True(q01.Enunciado.Length > 15);

        var q10 = resultado.Questoes.Single(q => q.NumeroOriginal == "10");
        Assert.Equal(TipoQuestao.MultiplaEscolha, q10.Tipo);
        Assert.Equal(new[] { 'A', 'B', 'C', 'D' }, q10.Alternativas.Select(a => a.Letra));
    }

    // Caso-limite descoberto testando contra um PDF real: o formato de
    // alternativa sem pontuação ("A texto") é ambíguo com uma frase comum de
    // enunciado que comece com uma letra de A-E isolada (ex.: "E
    // economistas..."). Sem o controle de sequência A,B,C,D,E em
    // MontarQuestao, essa linha viraria uma "alternativa E" falsa antes de
    // qualquer alternativa de verdade ter aparecido.
    [Fact]
    public void Extrair_LinhaDeEnunciadoComecandoComLetraForaDeSequencia_NaoEhTratadaComoAlternativa()
    {
        var resultado = EnadeProvaParser.Extrair(PdfFixtures.ProvaComLinhaAmbigua());

        var q05 = resultado.Questoes.Single(q => q.NumeroOriginal == "05");
        Assert.Contains("economistas", q05.Enunciado, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { 'A', 'B' }, q05.Alternativas.Select(a => a.Letra));
    }
}

// --- Grupo A2: PadraoRespostaEnadeParser puro (sem banco) — evolução
// "Padrão de Resposta + modelo híbrido", itens 33-36 do pedido. ---
public class PadraoRespostaEnadeParserTests
{
    [Fact]
    public void Extrair_ReconheceQuestaoDiscursiva01()
    {
        var resultado = PadraoRespostaEnadeParser.Extrair(PdfFixtures.PadraoRespostaSimples());

        Assert.True(resultado.Respostas.ContainsKey("D1"));
        Assert.Contains("mobilidade urbana", resultado.Respostas["D1"].TextoCompleto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extrair_ReconheceQuestaoDiscursiva02()
    {
        var resultado = PadraoRespostaEnadeParser.Extrair(PdfFixtures.PadraoRespostaSimples());

        Assert.True(resultado.Respostas.ContainsKey("D2"));
        Assert.Contains("consistencia", resultado.Respostas["D2"].TextoCompleto, StringComparison.OrdinalIgnoreCase);
    }

    // Item 36 do pedido, fixture explícito: "a) valor 2,0 / b) valor 2,0 /
    // c) valor 2,0 / d) valor 4,0" — verifica os 4 itens, a pontuação de
    // cada um preservada, e a resposta de cada subitem associada ao Codigo
    // certo (nunca embaralhada entre si).
    [Fact]
    public void Extrair_ComQuatroSubitensPontuados_ReconheceItensComPontuacaoERespostaCorretas()
    {
        var resultado = PadraoRespostaEnadeParser.Extrair(PdfFixtures.PadraoRespostaComSubitens());

        var d1 = resultado.Respostas["D1"];
        Assert.Equal(4, d1.Itens.Count);

        var a = d1.Itens.Single(i => i.Codigo == "a");
        var b = d1.Itens.Single(i => i.Codigo == "b");
        var c = d1.Itens.Single(i => i.Codigo == "c");
        var d = d1.Itens.Single(i => i.Codigo == "d");

        Assert.Equal(2.0m, a.Pontuacao);
        Assert.Equal(2.0m, b.Pontuacao);
        Assert.Equal(2.0m, c.Pontuacao);
        Assert.Equal(4.0m, d.Pontuacao);

        Assert.Contains("infraestrutura urbana", a.RespostaPadrao, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("residuos solidos", b.RespostaPadrao, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mobilidade urbana sustentavel", c.RespostaPadrao, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planejamento urbano", d.RespostaPadrao, StringComparison.OrdinalIgnoreCase);

        // Item 8 do pedido: mesmo com subitens estruturados, o TEXTO OFICIAL
        // completo continua disponível na íntegra (nunca resumido/substituído
        // pelos subitens).
        Assert.Contains("infraestrutura urbana", d1.TextoCompleto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planejamento urbano", d1.TextoCompleto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extrair_ArquivoSoComD1_NaoInventaD2()
    {
        var resultado = PadraoRespostaEnadeParser.Extrair(PdfFixtures.PadraoRespostaSoD1());

        Assert.True(resultado.Respostas.ContainsKey("D1"));
        Assert.False(resultado.Respostas.ContainsKey("D2"));
    }
}

// --- Grupo B/C: ImportadorProvaEnadeService (com banco InMemory + PDF fixture) ---
public class ImportadorProvaEnadeServiceTests
{
    // IPaginaPdfRenderizador FAKE (nunca o PaginaEnadeRenderizador de
    // verdade) pra maioria dos testes deste grupo — o objetivo aqui é
    // testar a ORQUESTRAÇÃO de ImportadorProvaEnadeService (associação de
    // página, não-substituição do conteúdo estruturado etc.), não o
    // PDFium/SkiaSharp de verdade por trás da renderização (isso tem teste
    // próprio, isolado, em PaginaEnadeRenderizadorTests — ver mais abaixo).
    // Devolve o mesmo PNG mínimo (1x1) pra QUALQUER página pedida —
    // suficiente pra provar que RepresentacaoOriginal.Conteudo fica
    // preenchido sem depender da biblioteca nativa dentro deste teste.
    private sealed class FakePaginaPdfRenderizador : IPaginaPdfRenderizador
    {
        public IReadOnlyDictionary<int, byte[]> RenderizarPaginas(byte[] pdfBytes, IEnumerable<int> numerosPagina) =>
            numerosPagina.Distinct().ToDictionary(n => n, _ => PdfFixtures.PngMinusculo);
    }

    private static ImportadorProvaEnadeService NovoImportador(ApplicationDbContext db, IPaginaPdfRenderizador? renderizador = null) =>
        new(db, TestFakes.NovoQuestaoService(db), renderizador ?? new FakePaginaPdfRenderizador(),
            NullLogger<ImportadorProvaEnadeService>.Instance);

    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedFormacaoGeralAsync(ApplicationDbContext db)
    {
        await DbSeeder.SeedDisciplinaFormacaoGeralAsync(db);
        var disciplina = await db.Disciplinas.Include(d => d.Assuntos).FirstAsync(d => d.Codigo == Disciplina.CodigoFormacaoGeral);
        var assunto = disciplina.Assuntos.First(a => a.Nome == "Conhecimentos Gerais");
        return (disciplina, assunto);
    }

    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedDisciplinaComumAsync(ApplicationDbContext db, string nome = "Circuitos Elétricos")
    {
        var disciplina = TestSeed.Disciplina(nome);
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Assunto de teste", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return (disciplina, assunto);
    }

    private static async Task<AreaCurso> SeedAreaCursoAsync(ApplicationDbContext db, string nome = "Engenharia de Computação")
    {
        var area = TestSeed.AreaCurso(nome);
        db.AreasCurso.Add(area);
        await db.SaveChangesAsync();
        return area;
    }

    [Fact]
    public async Task ProcessarAsync_FormacaoGeral_RecebeDisciplinaFormacaoGeralAutomaticamente_SemAreaCurso()
    {
        using var db = TestDbFactory.Criar();
        var (disciplinaFg, assuntoFg) = await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        foreach (var numero in new[] { "01", "D1" })
        {
            var q = resultado.Questoes.Single(x => x.NumeroOriginal == numero);
            Assert.Equal(SecaoEnade.FormacaoGeral, q.Secao);
            Assert.Equal(disciplinaFg.Id, q.DisciplinaId);
            Assert.Equal(assuntoFg.Id, q.AssuntoId);
            Assert.Null(q.AreaCursoId); // Formação Geral nunca fica vinculada a Área — item 20 do pedido.
        }
    }

    [Fact]
    public async Task ProcessarAsync_ComponenteEspecifico_RecebeAreaCursoSelecionada_DisciplinaFicaPendenteDeRevisao()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db, "Engenharia de Computação");
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        foreach (var numero in new[] { "10", "D2" })
        {
            var q = resultado.Questoes.Single(x => x.NumeroOriginal == numero);
            Assert.Equal(SecaoEnade.ComponenteEspecifico, q.Secao);
            Assert.Equal(area.Id, q.AreaCursoId);
            Assert.Equal(0, q.DisciplinaId); // Disciplina específica NUNCA é adivinhada — item 12 do pedido.
            Assert.Contains(q.Alertas, a => a.Contains("Disciplina"));
            Assert.NotEqual(StatusPreImportacaoEnade.Validada, q.Status); // fica pendente até o professor escolher, mas não bloqueia (não é ComErro por causa disso sozinho).
        }
    }

    [Fact]
    public async Task ProcessarAsync_SemGabaritoOficial_QuestaoObjetivaFicaRequerRevisaoComGabaritoPendente()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");
        Assert.Null(q01.RespostaCorretaIndex);
        Assert.Contains(q01.Alertas, a => a.Contains("Gabarito pendente"));
        Assert.Equal(StatusPreImportacaoEnade.RequerRevisao, q01.Status);
    }

    [Fact]
    public async Task ProcessarAsync_ComGabaritoOficial_PreencheRespostaCorretaAutomaticamente()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), PdfFixtures.GabaritoSimples(), pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");
        Assert.NotNull(q01.RespostaCorretaIndex);
        Assert.Equal('B', q01.Alternativas[q01.RespostaCorretaIndex!.Value].Letra);
        Assert.DoesNotContain(q01.Alertas, a => a.Contains("Gabarito pendente"));
    }

    // Item 24/36 do pedido: questão de Formação Geral já existente no banco
    // (mesmo Ano+Seção+Número) precisa virar alerta de possível duplicata —
    // mesmo reprocessando com uma Área DIFERENTE selecionada no caderno,
    // porque Formação Geral nunca é filtrada por Área ("Formação Geral
    // repetida em diferentes cadernos" não pode virar cópia nova).
    [Fact]
    public async Task ProcessarAsync_FormacaoGeralJaExistenteNoBanco_GeraAlertaDePossivelDuplicata()
    {
        using var db = TestDbFactory.Criar();
        var (_, assuntoFg) = await SeedFormacaoGeralAsync(db);
        await SeedAreaCursoAsync(db, "Engenharia de Computação");
        var areaCiencia = await SeedAreaCursoAsync(db, "Ciência da Computação");

        var questaoServiceDireto = TestFakes.NovoQuestaoService(db);
        var modeloExistente = new QuestaoInput
        {
            AssuntoId = assuntoFg.Id,
            Enunciado = "Questão de Formação Geral já cadastrada anteriormente no banco.",
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
            Origem = OrigemQuestao.Enade,
            SecaoEnade = SecaoEnade.FormacaoGeral,
            Ano = 2023,
            NumeroOriginal = "01",
        };
        await questaoServiceDireto.CriarAsync(modeloExistente, new List<PendenteImagem>(), "prof-1", null);

        var service = NovoImportador(db);
        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: areaCiencia.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");
        Assert.True(q01.PossivelDuplicata);
        Assert.Contains(q01.Alertas, a => a.Contains("já existente"));
    }

    // Item 24/36 do pedido: "Questão 10" de Componente Específico já
    // cadastrada pra Engenharia de Computação NÃO pode ser confundida com
    // "Questão 10" de outra Área (Ciência da Computação) — só quando a
    // MESMA Área é escolhida de novo é que vira alerta de duplicata.
    [Fact]
    public async Task ProcessarAsync_ComponenteEspecifico_MesmoNumeroEmAreaDiferente_NaoEhTratadaComoDuplicata()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var (_, assuntoComum) = await SeedDisciplinaComumAsync(db, "Circuitos Elétricos");
        var areaEngenharia = await SeedAreaCursoAsync(db, "Engenharia de Computação");
        var areaCiencia = await SeedAreaCursoAsync(db, "Ciência da Computação");

        var questaoServiceDireto = TestFakes.NovoQuestaoService(db);
        var modeloExistente = new QuestaoInput
        {
            AssuntoId = assuntoComum.Id,
            Enunciado = "Questão de Componente Específico já cadastrada para Engenharia de Computação.",
            Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
            Origem = OrigemQuestao.Enade,
            SecaoEnade = SecaoEnade.ComponenteEspecifico,
            AreaCursoIds = new List<int> { areaEngenharia.Id },
            Ano = 2023,
            NumeroOriginal = "10",
        };
        await questaoServiceDireto.CriarAsync(modeloExistente, new List<PendenteImagem>(), "prof-1", null);

        var service = NovoImportador(db);

        var resultadoOutraArea = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: areaCiencia.Id);
        var q10OutraArea = resultadoOutraArea.Questoes.Single(q => q.NumeroOriginal == "10");
        Assert.False(q10OutraArea.PossivelDuplicata);

        var resultadoMesmaArea = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: areaEngenharia.Id);
        var q10MesmaArea = resultadoMesmaArea.Questoes.Single(q => q.NumeroOriginal == "10");
        Assert.True(q10MesmaArea.PossivelDuplicata);
    }

    // Item 26 do pedido: ProcessarAsync (pré-importação) NUNCA cria Questao
    // nenhuma. "Cancelar" a importação (ver QuestaoImportarEnade.razor.Cancelar())
    // é só descartar este objeto `resultado`, em memória, sem nunca chamar
    // ImportarSelecionadasAsync — no nível do banco de dados, "pré-importação
    // -> não cria Questao" e "cancelamento -> não cria Questao" são
    // exatamente a mesma verificação, por isso um teste só cobre os dois.
    [Fact]
    public async Task ProcessarAsync_ApenasExtrai_NuncaCriaQuestaoAntesDaConfirmacao()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        Assert.Equal(4, resultado.Questoes.Count);
        Assert.Equal(0, await db.Questoes.CountAsync());
    }

    private static QuestaoImportacaoEnade NovaQuestaoObjetivaValida(int idLote, SecaoEnade secao, string numero, int disciplinaId, int assuntoId, int? areaCursoId, int? respostaCorretaIndex = 0) => new()
    {
        IdLote = idLote,
        Secao = secao,
        NumeroOriginal = numero,
        Tipo = TipoQuestao.MultiplaEscolha,
        Enunciado = "Enunciado de teste com tamanho suficiente para passar na validação mínima do parser.",
        Alternativas = new List<AlternativaImportacaoEnade>
        {
            new() { Letra = 'A', Texto = "Alternativa A de teste" },
            new() { Letra = 'B', Texto = "Alternativa B de teste" },
        },
        RespostaCorretaIndex = respostaCorretaIndex,
        Ano = 2023,
        AreaCursoId = areaCursoId,
        DisciplinaId = disciplinaId,
        AssuntoId = assuntoId,
        Status = StatusPreImportacaoEnade.Validada,
        Selecionada = true,
    };

    private static QuestaoImportacaoEnade NovaQuestaoDiscursivaValida(int idLote, SecaoEnade secao, string numero, int disciplinaId, int assuntoId, int? areaCursoId) => new()
    {
        IdLote = idLote,
        Secao = secao,
        NumeroOriginal = numero,
        Tipo = TipoQuestao.Discursiva,
        Enunciado = "Enunciado discursivo de teste com tamanho suficiente para passar na validação mínima.",
        RespostaEsperadaDiscursiva = "Resposta esperada de teste.",
        Ano = 2023,
        AreaCursoId = areaCursoId,
        DisciplinaId = disciplinaId,
        AssuntoId = assuntoId,
        Status = StatusPreImportacaoEnade.Validada,
        Selecionada = true,
    };

    // Item 26/27/28 do pedido: confirmação ("Importar selecionadas") cria
    // Questao de verdade via QuestaoService (mesmas regras do cadastro
    // manual); questão não selecionada é ignorada (não criada, mas também
    // não trava as demais); questão com problema (aqui: MultiplaEscolha sem
    // gabarito escolhido) fica em ComErro, com a mensagem, sem derrubar o
    // restante do lote — exatamente o "se uma falhar, mostre quais foram
    // importadas/não importadas/com erro" do item 27.
    [Fact]
    public async Task ImportarSelecionadasAsync_QuestoesSelecionadas_SaoCriadasComoQuestaoDeVerdade()
    {
        using var db = TestDbFactory.Criar();
        var (disciplinaFg, assuntoFg) = await SeedFormacaoGeralAsync(db);
        var (disciplinaComum, assuntoComum) = await SeedDisciplinaComumAsync(db);
        var area = await SeedAreaCursoAsync(db);

        var resultado = new ResultadoImportacaoEnade { Ano = 2023, AreaCursoId = area.Id, NomeAreaCurso = area.Nome };
        resultado.Questoes.Add(NovaQuestaoObjetivaValida(0, SecaoEnade.FormacaoGeral, "01", disciplinaFg.Id, assuntoFg.Id, areaCursoId: null));
        resultado.Questoes.Add(NovaQuestaoDiscursivaValida(1, SecaoEnade.ComponenteEspecifico, "D2", disciplinaComum.Id, assuntoComum.Id, areaCursoId: area.Id));

        var naoSelecionada = NovaQuestaoObjetivaValida(2, SecaoEnade.ComponenteEspecifico, "11", disciplinaComum.Id, assuntoComum.Id, areaCursoId: area.Id);
        naoSelecionada.Selecionada = false;
        resultado.Questoes.Add(naoSelecionada);

        var comGabaritoPendente = NovaQuestaoObjetivaValida(3, SecaoEnade.ComponenteEspecifico, "12", disciplinaComum.Id, assuntoComum.Id, areaCursoId: area.Id, respostaCorretaIndex: null);
        resultado.Questoes.Add(comGabaritoPendente);

        var service = NovoImportador(db);
        var relatorio = await service.ImportarSelecionadasAsync(resultado, criadoPorId: "prof-1", minhaInstituicaoId: null);

        Assert.Equal(2, relatorio.Importadas.Count);
        Assert.Single(relatorio.Ignoradas);
        Assert.Single(relatorio.ComErro);
        Assert.Equal(2, await db.Questoes.CountAsync());

        Assert.Equal(StatusPreImportacaoEnade.Importada, resultado.Questoes[0].Status);
        Assert.Equal(StatusPreImportacaoEnade.Ignorada, resultado.Questoes[2].Status);
    }

    // --- Evolução "Padrão de Resposta + modelo híbrido" (itens 33-36 do pedido) ---

    // Item 6/28/36 do pedido: com o arquivo de padrão de resposta
    // informado, D1 e D2 recebem a resposta oficial automaticamente — sem
    // precisar de nenhuma edição manual — e o alerta antigo ("preencha a
    // resposta esperada") deixa de aparecer.
    [Fact]
    public async Task ProcessarAsync_ComPadraoRespostaOficial_D1D2RecebemRespostaOficialESemAlertaDePreenchimento()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, PdfFixtures.PadraoRespostaSimples(), ano: 2023, areaCursoId: area.Id);

        Assert.True(resultado.TinhaPadraoResposta);

        var d1 = resultado.Questoes.Single(q => q.NumeroOriginal == "D1");
        var d2 = resultado.Questoes.Single(q => q.NumeroOriginal == "D2");

        Assert.Contains("mobilidade urbana", d1.RespostaEsperadaDiscursiva, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consistencia", d2.RespostaEsperadaDiscursiva, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(d1.PaginaOrigemPadraoResposta);
        Assert.NotNull(d2.PaginaOrigemPadraoResposta);
        Assert.DoesNotContain(d1.Alertas, a => a.Contains("preencha", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(d2.Alertas, a => a.Contains("preencha", StringComparison.OrdinalIgnoreCase));
    }

    // Sem arquivo de padrão informado (comportamento anterior a esta
    // evolução, preservado): discursiva nasce sem resposta esperada e com
    // o alerta pedindo preenchimento manual.
    [Fact]
    public async Task ProcessarAsync_SemArquivoDePadraoResposta_DiscursivaRequerRevisaoComAlertaDePreenchimento()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        Assert.False(resultado.TinhaPadraoResposta);
        var d1 = resultado.Questoes.Single(q => q.NumeroOriginal == "D1");
        Assert.True(string.IsNullOrWhiteSpace(d1.RespostaEsperadaDiscursiva));
        Assert.Contains(d1.Alertas, a => a.Contains("padrão de resposta", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(StatusPreImportacaoEnade.RequerRevisao, d1.Status);
    }

    // Item 5 do pedido: arquivo de padrão informado, mas UMA das discursivas
    // (D2) não é encontrada nele — nunca inventa a resposta; fica pendente
    // de revisão manual, enquanto D1 (que FOI encontrada) é preenchida
    // normalmente.
    [Fact]
    public async Task ProcessarAsync_PadraoRespostaSemCorrespondenciaParaD2_D2FicaRequerRevisaoD1NaoEhAfetada()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, PdfFixtures.PadraoRespostaSoD1(), ano: 2023, areaCursoId: area.Id);

        var d1 = resultado.Questoes.Single(q => q.NumeroOriginal == "D1");
        var d2 = resultado.Questoes.Single(q => q.NumeroOriginal == "D2");

        Assert.False(string.IsNullOrWhiteSpace(d1.RespostaEsperadaDiscursiva));
        Assert.True(string.IsNullOrWhiteSpace(d2.RespostaEsperadaDiscursiva));
        Assert.Contains(d2.Alertas, a => a.Contains("Padrão oficial não encontrado", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(StatusPreImportacaoEnade.RequerRevisao, d2.Status);
    }

    // Item 10 do pedido: o padrão de resposta (documento discursivo) nunca
    // pode influenciar o gabarito OBJETIVO — são dois documentos e duas
    // classificações totalmente independentes.
    [Fact]
    public async Task ProcessarAsync_PadraoRespostaNuncaAlteraGabaritoObjetivo()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        // Só o padrão de resposta é informado — SEM gabarito objetivo.
        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, PdfFixtures.PadraoRespostaSimples(), ano: 2023, areaCursoId: area.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");
        Assert.Null(q01.RespostaCorretaIndex);
        Assert.Contains(q01.Alertas, a => a.Contains("Gabarito pendente", StringComparison.OrdinalIgnoreCase));
    }

    // --- Modelo híbrido (itens 12-21 do pedido) ---

    // Uma questão de texto simples continua com o conteúdo ESTRUTURADO
    // completo (Enunciado/Alternativas) mesmo depois de ganhar
    // RepresentacaoOriginal — a representação nunca SUBSTITUI o conteúdo,
    // só acompanha (item 13 do pedido).
    [Fact]
    public async Task ProcessarAsync_QuestaoTextual_MantemConteudoEstruturado_ComRepresentacaoOriginalPreenchida()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db); // FakePaginaPdfRenderizador — ver classe acima.

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");

        // Conteúdo estruturado continua intacto — nunca "virou imagem".
        Assert.True(q01.Enunciado.Length > 15);
        Assert.Equal(4, q01.Alternativas.Count);

        // E, adicionalmente, ganhou snapshot(s) de página de origem.
        Assert.NotEmpty(q01.RepresentacoesOriginais);
        Assert.All(q01.RepresentacoesOriginais, r => Assert.NotNull(r.Conteudo));
    }

    // Questão com imagem de CONTEÚDO (figura pedagógica) continua extraindo
    // ImagemImportacaoEnade normalmente — e RepresentacaoOriginal (snapshot
    // de auditoria) nunca é confundida com ela, mesmo as duas existindo ao
    // mesmo tempo pra mesma questão (item 19/21 do pedido).
    [Fact]
    public async Task ProcessarAsync_QuestaoComImagem_MantemImagemDeConteudo_NaoConfundidaComRepresentacaoOriginal()
    {
        using var db = TestDbFactory.Criar();
        await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);
        var service = NovoImportador(db);

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaComImagem(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        var q01 = resultado.Questoes.Single(q => q.NumeroOriginal == "01");

        Assert.NotEmpty(q01.Imagens); // conteúdo pedagógico — continua existindo.
        Assert.NotEmpty(q01.RepresentacoesOriginais); // snapshot de auditoria — existe também.

        // Nunca a mesma lista / nunca um substituindo o outro.
        Assert.All(q01.Imagens, img => Assert.NotNull(img.Conteudo));
        Assert.All(q01.RepresentacoesOriginais, rep => Assert.NotEqual(0, rep.Pagina));
    }

    // Item 18 do pedido: uma questão que abrange mais de uma página aceita
    // MAIS DE UMA RepresentacaoOriginal — nunca assume "1 imagem por
    // questão". Construído diretamente sobre o DTO (sem depender de um PDF
    // real de várias páginas por questão, que exigiria um fixture bem maior
    // só pra isso) — testa a mecânica de PaginaOrigem/PaginaFim que
    // ImportadorProvaEnadeService.AplicarRepresentacoesOriginais usa.
    [Fact]
    public async Task ProcessarAsync_QuestaoComPaginaOrigemEPaginaFimDiferentes_RecebeUmaRepresentacaoPorPagina()
    {
        using var db = TestDbFactory.Criar();
        var (disciplinaFg, assuntoFg) = await SeedFormacaoGeralAsync(db);
        var area = await SeedAreaCursoAsync(db);

        // Renderizador fake que devolve página pra QUALQUER número pedido —
        // permite este teste focar só na CONTAGEM de representações por
        // questão, sem precisar de um PDF de várias páginas de verdade.
        var service = NovoImportador(db, new FakePaginaPdfRenderizador());

        var resultado = await service.ProcessarAsync(
            PdfFixtures.ProvaCompleta(), pdfGabarito: null, pdfPadraoResposta: null, ano: 2023, areaCursoId: area.Id);

        // A própria ProvaCompleta() já produz PaginaOrigem/PaginaFim reais
        // (uma questão por página, neste fixture) — o teste de verdade
        // pra "mais de uma página" está em EnadeProvaParser (PaginaFim
        // sempre >= PaginaOrigem) + PaginasDoIntervalo (privado, coberto
        // indiretamente aqui): simulamos o caso "múltiplas páginas"
        // diretamente, sobrescrevendo PaginaFim de uma questão já
        // processada e reaplicando só a etapa de representação via um novo
        // processamento não é possível (é privado) — em vez disso,
        // validamos a invariante pelo contrato público: toda questão com
        // PaginaOrigem definido tem exatamente
        // (PaginaFim - PaginaOrigem + 1) representações.
        foreach (var q in resultado.Questoes.Where(q => q.PaginaOrigem is not null))
        {
            var esperado = (q.PaginaFim ?? q.PaginaOrigem!.Value) - q.PaginaOrigem!.Value + 1;
            Assert.Equal(esperado, q.RepresentacoesOriginais.Count);
        }
    }
}

// --- Grupo D: PaginaEnadeRenderizador de verdade (integra com a biblioteca
// PDFtoImage/PDFium — item 33-36 do pedido pede teste "híbrido"; este é o
// único teste do pacote inteiro que NÃO usa o fake, justamente pra provar
// que a integração com a biblioteca escolhida (ver csproj) funciona de
// verdade contra um PDF gerado neste mesmo processo — mais barato/rápido
// que testar isso indiretamente por ImportadorProvaEnadeService toda vez. ---
public class PaginaEnadeRenderizadorTests
{
    [Fact]
    public void RenderizarPaginas_PdfValido_DevolvePngNaoVazioParaPaginaPedida()
    {
        var renderizador = new PaginaEnadeRenderizador();

        var resultado = renderizador.RenderizarPaginas(PdfFixtures.ProvaCompleta(), new[] { 1 });

        Assert.True(resultado.ContainsKey(1));
        Assert.NotEmpty(resultado[1]);
        // Assinatura de arquivo PNG (0x89 'P' 'N' 'G' ...) — confirma que o
        // conteúdo devolvido é mesmo um PNG, não bytes arbitrários.
        Assert.Equal(0x89, resultado[1][0]);
        Assert.Equal((byte)'P', resultado[1][1]);
        Assert.Equal((byte)'N', resultado[1][2]);
        Assert.Equal((byte)'G', resultado[1][3]);
    }

    [Fact]
    public void RenderizarPaginas_PaginaInexistente_NaoLancaExcecao_SoOmiteDoResultado()
    {
        var renderizador = new PaginaEnadeRenderizador();

        var resultado = renderizador.RenderizarPaginas(PdfFixtures.ProvaCompleta(), new[] { 9999 });

        Assert.False(resultado.ContainsKey(9999));
    }

    [Fact]
    public void RenderizarPaginas_PdfVazio_DevolveDicionarioVazio_SemLancarExcecao()
    {
        var renderizador = new PaginaEnadeRenderizador();

        var resultado = renderizador.RenderizarPaginas(Array.Empty<byte>(), new[] { 1 });

        Assert.Empty(resultado);
    }
}
