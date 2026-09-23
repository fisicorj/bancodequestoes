using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BancoQuestoes.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BancoQuestoes.Importacao;

// Parser/extrator de provas ENADE: só texto/bytes entrando, DTOs saindo, nunca toca
// o banco. Determinístico (regex + bounding box, sem IA); sem certeza, marca Alerta.
public static class EnadeProvaParser
{
    // "QUESTÃO 04" inicia objetiva; "QUESTÃO DISCURSIVA 1" discursiva. numD aceita
    // 1-2 dígitos (cabeçalho real usa "01"), zero removido depois em MontarQuestao.
    private static readonly Regex QuestaoBoundaryRegex = new(
        @"QUEST(?:Ã|A)O\s+DISCURSIVA\s+(?<numD>\d{1,2})\b|QUEST(?:Ã|A)O\s+(?<num>\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FormacaoGeralHeaderRegex = new(
        @"FORMA(?:Ç|C)(?:Ã|A)O\s+GERAL", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ComponenteEspecificoHeaderRegex = new(
        @"COMPONENTE\s+ESPEC(?:Í|I)FICO", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Questionário de autoavaliação do estudante sobre a prova em si (extensão,
    // dificuldade, clareza...), sempre a última seção do caderno — NUNCA é
    // conteúdo/questão de verdade, então tem que ser cortado antes de qualquer
    // classificação de seção rodar, senão vira "questão" fantasma no import.
    private static readonly Regex QuestionarioPercepcaoHeaderRegex = new(
        @"QUESTION(?:Á|A)RIO\s+DE\s+PERCEP(?:Ç|C)(?:Ã|A)O", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "(A)", "A)", "A." no início da linha — aceita ponto ou parêntese como
    // separador, pois o ENADE varia o estilo tipográfico entre edições.
    private static readonly Regex AlternativaComPontuacaoRegex = new(
        @"^\(?(?<letra>[A-E])\)?[\.\)]\s*(?<resto>.*)$", RegexOptions.Compiled);

    // Formato sem pontuação: a "bolinha" é gráfica, sobra só "A texto". Ambíguo, só
    // aceito quando a letra bate com a próxima esperada A,B,C,D,E (ver MontarQuestao).
    private static readonly Regex AlternativaSemPontuacaoRegex = new(
        @"^(?<letra>[A-E])\s+(?<resto>\S.*)$", RegexOptions.Compiled);

    // Palavras que, sem imagem associada à questão, sugerem que falta uma figura/tabela.
    private static readonly string[] PalavrasSugeremImagem =
        { "figura", "gráfico", "grafico", "tabela", "diagrama", "circuito", "esquema", "imagem abaixo", "quadro abaixo" };

    // Marcadores estruturais ("TEXTO 1", "I.", "PORQUE") que viram parágrafos
    // separados — usado em MontarTextoComQuebras pra decidir onde quebrar.
    private static readonly Regex LinhaComecaComRomanoOuTextoRegex = new(
        @"^(?:[IVX]{1,4}\.\s|TEXTO\s+(?:\d+|[IVX]+)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string MarcadorPagina = "[[PAGINA:";

    // "logar" (opcional): hook de diagnóstico — cada linha processada por MontarQuestao
    // chama com sua classificação e motivo, sem afetar o resultado da extração.
    public static ResultadoImportacaoEnade Extrair(byte[] pdfBytes, Action<string>? logar = null)
    {
        var resultado = new ResultadoImportacaoEnade();

        PdfDocument documento;
        try
        {
            documento = PdfDocument.Open(pdfBytes);
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/inválido: nunca deixa a exceção estourar até a tela;
            // quem chama decide se aborta com este alerta como único resultado.
            resultado.AlertasGerais.Add($"Não foi possível ler o PDF da prova: {MensagemAmigavel(ex)}");
            return resultado;
        }

        using (documento)
        {
            if (documento.NumberOfPages == 0)
            {
                resultado.AlertasGerais.Add("O PDF da prova não tem nenhuma página.");
                return resultado;
            }

            var paginas = new List<(int Numero, string Texto)>();
            var imagensPorPagina = new Dictionary<int, List<ImagemImportacaoEnade>>();

            foreach (var pagina in documento.GetPages())
            {
                string texto;
                try
                {
                    texto = TextoEmOrdemDeLeitura(pagina);
                }
                catch
                {
                    // Página "estranha" nunca derruba o processamento inteiro —
                    // cai pro extrator simples do próprio PdfPig.
                    texto = SeguroPageText(pagina);
                }

                paginas.Add((pagina.Number, texto));
                imagensPorPagina[pagina.Number] = ExtrairImagensDaPagina(pagina, logar);
            }

            RemoverImagensFantasmaDuplicadas(imagensPorPagina);

            paginas = RemoverQuestionarioPercepcao(paginas, logar);

            // Propaga "a última seção vista até aqui" página a página: uma vez visto
            // "COMPONENTE ESPECÍFICO", toda página seguinte é dessa seção.
            var secaoPorPagina = ClassificarSecaoPorPagina(paginas, resultado.AlertasGerais);

            var textoFormacaoGeral = ConcatenarPaginas(paginas, secaoPorPagina, SecaoEnade.FormacaoGeral);
            var textoComponenteEspecifico = ConcatenarPaginas(paginas, secaoPorPagina, SecaoEnade.ComponenteEspecifico);

            var questoes = new List<QuestaoImportacaoEnade>();
            questoes.AddRange(ExtrairQuestoesDaSecao(textoFormacaoGeral, SecaoEnade.FormacaoGeral, secaoPorPagina, imagensPorPagina, logar));
            questoes.AddRange(ExtrairQuestoesDaSecao(textoComponenteEspecifico, SecaoEnade.ComponenteEspecifico, secaoPorPagina, imagensPorPagina, logar));

            // Ordena Formação Geral antes de Componente Específico, e por página
            // dentro de cada seção — só cosmético, pra bater com o caderno impresso.
            questoes = questoes
                .OrderBy(q => q.Secao == SecaoEnade.FormacaoGeral ? 0 : 1)
                .ThenBy(q => q.PaginaOrigem ?? int.MaxValue)
                .ToList();

            for (var i = 0; i < questoes.Count; i++)
            {
                questoes[i].IdLote = i;
            }

            MarcarDuplicidadeDentroDoLote(questoes);

            foreach (var q in questoes)
            {
                ClassificarStatusInicial(q);
            }

            resultado.Questoes = questoes;
        }

        return resultado;
    }

    // Corta o texto no início do Questionário de Percepção da Prova e descarta
    // as páginas inteiramente posteriores; mantém o que vier ANTES do cabeçalho
    // na mesma página (pode ser o fim de uma questão real, quando o questionário
    // começa na mesma página da última questão do caderno).
    private static List<(int Numero, string Texto)> RemoverQuestionarioPercepcao(
        List<(int Numero, string Texto)> paginas, Action<string>? logar = null)
    {
        var resultado = new List<(int Numero, string Texto)>();
        foreach (var (numero, texto) in paginas)
        {
            var match = QuestionarioPercepcaoHeaderRegex.Match(texto);
            if (match.Success)
            {
                var textoAntes = texto[..match.Index].TrimEnd();
                logar?.Invoke($"[PercepcaoProva] Questionário de Percepção da Prova encontrado na página {numero} — essa página (a partir do cabeçalho) e todas as seguintes foram ignoradas.");
                if (textoAntes.Length > 0)
                {
                    resultado.Add((numero, textoAntes));
                }

                break;
            }

            resultado.Add((numero, texto));
        }

        return resultado;
    }

    private static string MensagemAmigavel(Exception ex) =>
        ex.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            ? "o arquivo está protegido por senha."
            : "o arquivo parece corrompido ou não é um PDF válido.";

    private static string SeguroPageText(Page pagina)
    {
        try { return pagina.Text; }
        catch { return ""; }
    }

    // Propaga a última seção vista, com correções documentadas nos Alertas: sem
    // "FORMAÇÃO GERAL", páginas antes viram Formação Geral; sem cabeçalho, tudo vira Componente Específico.
    private static Dictionary<int, SecaoEnade?> ClassificarSecaoPorPagina(
        List<(int Numero, string Texto)> paginas, List<string> alertasGerais)
    {
        var porPagina = new Dictionary<int, SecaoEnade?>();
        SecaoEnade? atual = null;
        var achouFormacaoGeral = false;
        var achouComponenteEspecifico = false;
        var primeiraPaginaComponenteEspecifico = int.MaxValue;

        foreach (var (numero, texto) in paginas)
        {
            var posFg = FormacaoGeralHeaderRegex.Match(texto);
            var posCe = ComponenteEspecificoHeaderRegex.Match(texto);

            if (posFg.Success && posCe.Success)
            {
                // As duas aparecem na mesma página (raro) — usa quem aparece primeiro.
                atual = posFg.Index < posCe.Index ? SecaoEnade.FormacaoGeral : SecaoEnade.ComponenteEspecifico;
            }
            else if (posFg.Success)
            {
                atual = SecaoEnade.FormacaoGeral;
            }
            else if (posCe.Success)
            {
                atual = SecaoEnade.ComponenteEspecifico;
            }

            if (posFg.Success) { achouFormacaoGeral = true; }
            if (posCe.Success)
            {
                achouComponenteEspecifico = true;
                primeiraPaginaComponenteEspecifico = Math.Min(primeiraPaginaComponenteEspecifico, numero);
            }

            porPagina[numero] = atual;
        }

        if (!achouFormacaoGeral && !achouComponenteEspecifico)
        {
            alertasGerais.Add(
                "Não foi possível identificar as seções \"Formação Geral\" e \"Componente Específico\" no PDF — " +
                "todas as questões foram tratadas como Componente Específico. Revise manualmente a Seção de cada questão.");
            foreach (var numero in porPagina.Keys.ToList())
            {
                porPagina[numero] = SecaoEnade.ComponenteEspecifico;
            }
        }
        else if (!achouFormacaoGeral && achouComponenteEspecifico)
        {
            alertasGerais.Add(
                "Não foi possível identificar o início da seção \"Formação Geral\" — as páginas antes de " +
                "\"Componente Específico\" foram tratadas como Formação Geral (ordem padrão do ENADE). Revise manualmente.");
            foreach (var numero in porPagina.Keys.ToList())
            {
                if (numero < primeiraPaginaComponenteEspecifico)
                {
                    porPagina[numero] = SecaoEnade.FormacaoGeral;
                }
            }
        }

        return porPagina;
    }

    private static string ConcatenarPaginas(
        List<(int Numero, string Texto)> paginas, Dictionary<int, SecaoEnade?> secaoPorPagina, SecaoEnade secao) =>
        string.Join("\n\n", paginas
            .Where(p => secaoPorPagina.GetValueOrDefault(p.Numero) == secao)
            .Select(p => $"{MarcadorPagina}{p.Numero}]]\n{p.Texto}"));

    private static List<QuestaoImportacaoEnade> ExtrairQuestoesDaSecao(
        string textoSecao, SecaoEnade secao, Dictionary<int, SecaoEnade?> secaoPorPagina,
        Dictionary<int, List<ImagemImportacaoEnade>> imagensPorPagina, Action<string>? logar = null)
    {
        var questoes = new List<QuestaoImportacaoEnade>();
        if (string.IsNullOrWhiteSpace(textoSecao))
        {
            return questoes;
        }

        var matches = QuestaoBoundaryRegex.Matches(textoSecao);
        if (matches.Count == 0)
        {
            return questoes;
        }

        var paginasDaSecao = secaoPorPagina.Where(kv => kv.Value == secao).Select(kv => kv.Key).ToList();
        var ultimaPaginaDaSecao = paginasDaSecao.Count > 0 ? paginasDaSecao.Max() : (int?)null;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var fimDoCorpo = i + 1 < matches.Count ? matches[i + 1].Index : textoSecao.Length;
            var corpo = textoSecao[match.Index..fimDoCorpo];
            // Remove o cabeçalho ("QUESTÃO 13") do início antes de interpretar o resto.
            corpo = corpo[match.Length..];

            // Remove o zero à esquerda do número discursivo ("01" -> "D1") pra manter
            // a convenção "D1"/"D2" (nunca "D01") mesmo casando com o cabeçalho real.
            var numero = match.Groups["numD"].Success
                ? $"D{int.Parse(match.Groups["numD"].Value)}"
                : match.Groups["num"].Value.PadLeft(2, '0');
            var tipoTentativo = match.Groups["numD"].Success ? TipoQuestao.Discursiva : TipoQuestao.MultiplaEscolha;

            var paginaOrigem = PaginaAntesDe(textoSecao, match.Index) ?? paginasDaSecao.FirstOrDefault();
            var proximaPagina = i + 1 < matches.Count
                ? (PaginaAntesDe(textoSecao, matches[i + 1].Index) ?? paginaOrigem)
                : (ultimaPaginaDaSecao ?? paginaOrigem);

            var questao = MontarQuestao(numero, tipoTentativo, corpo, paginaOrigem, secao, logar);
            // Mesmo intervalo [paginaOrigem, proximaPagina] usado pra achar imagens,
            // reaproveitado pro modelo híbrido (RepresentacaoOriginal por página).
            questao.PaginaFim = proximaPagina;
            questao.Imagens = ImagensNoIntervalo(imagensPorPagina, paginaOrigem, proximaPagina, secaoPorPagina, secao);

            if (SugereImagemFaltando(questao))
            {
                questao.Alertas.Add("O enunciado menciona figura/gráfico/tabela, mas nenhuma imagem foi associada — verifique se falta anexar manualmente.");
            }

            questoes.Add(questao);
        }

        return questoes;
    }

    private static int? PaginaAntesDe(string texto, int posicao)
    {
        var ultimoIndice = texto.LastIndexOf(MarcadorPagina, Math.Min(posicao, texto.Length) - 1, StringComparison.Ordinal);
        if (ultimoIndice < 0)
        {
            return null;
        }

        var inicioNumero = ultimoIndice + MarcadorPagina.Length;
        var fimNumero = texto.IndexOf(']', inicioNumero);
        if (fimNumero < 0)
        {
            return null;
        }

        return int.TryParse(texto[inicioNumero..fimNumero], out var numero) ? numero : null;
    }

    private static QuestaoImportacaoEnade MontarQuestao(
        string numero, TipoQuestao tipoTentativo, string corpo, int? paginaOrigem, SecaoEnade secao, Action<string>? logar = null)
    {
        var questao = new QuestaoImportacaoEnade
        {
            NumeroOriginal = numero,
            Secao = secao,
            PaginaOrigem = paginaOrigem,
            Tipo = tipoTentativo,
        };

        var linhas = corpo.Replace("\r\n", "\n").Split('\n')
            .Where(l => !l.TrimStart().StartsWith(MarcadorPagina, StringComparison.Ordinal))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var enunciadoLinhas = new List<string>();
        var alternativas = new List<AlternativaImportacaoEnade>();
        AlternativaImportacaoEnade? atual = null;
        // Controla a sequência esperada A,B,C,D,E — só usado pra validar o formato
        // sem pontuação; o formato com pontuação nunca precisa dessa checagem.
        var proximaLetraEsperada = 'A';

        // Salvaguarda contra "enunciado virando alternativa": sem confirmação, uma nova
        // linha "A texto" repetindo a mesma letra desfaz a tentativa anterior e recomeça.
        var sequenciaConfirmada = false;
        var linhasDaTentativaNaoConfirmada = new List<string>();

        foreach (var linha in linhas)
        {
            char? letraDetectada = null;
            string? restoDetectado = null;
            string motivo; // só usado se "logar" não for null — ver fim do laço.

            var mPontuada = AlternativaComPontuacaoRegex.Match(linha);
            if (mPontuada.Success)
            {
                letraDetectada = char.ToUpperInvariant(mPontuada.Groups["letra"].Value[0]);
                restoDetectado = mPontuada.Groups["resto"].Value.Trim();
                motivo = "bateu formato COM pontuação — ex.: \"(A)\"/\"A)\"/\"A.\"";
                // Nunca ambíguo — confirma a sequência de imediato, mesmo na primeira letra.
                sequenciaConfirmada = true;
            }
            else
            {
                var mSemPontuacao = AlternativaSemPontuacaoRegex.Match(linha);
                if (mSemPontuacao.Success)
                {
                    var letraCandidata = char.ToUpperInvariant(mSemPontuacao.Groups["letra"].Value[0]);
                    var repeteTentativaNaoConfirmada =
                        !sequenciaConfirmada && alternativas.Count == 1 && letraCandidata == alternativas[0].Letra;

                    if (repeteTentativaNaoConfirmada)
                    {
                        // Desfaz a tentativa anterior: suas linhas voltam pro
                        // enunciado, e a sequência recomeça a partir desta linha.
                        enunciadoLinhas.AddRange(linhasDaTentativaNaoConfirmada);
                        linhasDaTentativaNaoConfirmada.Clear();
                        alternativas.Clear();
                        atual = null;
                        proximaLetraEsperada = 'A';

                        letraDetectada = letraCandidata;
                        restoDetectado = mSemPontuacao.Groups["resto"].Value.Trim();
                        motivo = $"bateu formato SEM pontuação (\"{letraCandidata} texto\") — substitui uma tentativa anterior de alternativa \"{letraCandidata}\" que nunca foi confirmada por uma alternativa \"{(char)(letraCandidata + 1)}\" em sequência (provável falso positivo de frase de enunciado começando com \"{letraCandidata} \")";
                    }
                    else if (letraCandidata == proximaLetraEsperada)
                    {
                        letraDetectada = letraCandidata;
                        restoDetectado = mSemPontuacao.Groups["resto"].Value.Trim();
                        motivo = $"bateu formato SEM pontuação (\"{letraCandidata} texto\") e a letra bate com a próxima esperada ({proximaLetraEsperada})";

                        if (!sequenciaConfirmada && alternativas.Count > 0)
                        {
                            // Segunda letra aceita em sequência — confirma; não resetamos mais depois disso.
                            sequenciaConfirmada = true;
                        }
                    }
                    else
                    {
                        motivo = $"bateu formato SEM pontuação, mas a letra \"{letraCandidata}\" NÃO é a próxima esperada (\"{proximaLetraEsperada}\") — tratada como continuação/enunciado pra evitar falso positivo (ver AlternativaSemPontuacaoRegex)";
                    }
                }
                else
                {
                    motivo = "não bateu nenhum formato de alternativa";
                }
            }

            if (letraDetectada is char letra)
            {
                atual = new AlternativaImportacaoEnade { Letra = letra, Texto = restoDetectado ?? "" };
                alternativas.Add(atual);
                if (letra == proximaLetraEsperada)
                {
                    proximaLetraEsperada = (char)(letra + 1);
                }

                if (!sequenciaConfirmada)
                {
                    linhasDaTentativaNaoConfirmada.Add(linha);
                }

                logar?.Invoke($"Questão {numero}: linha \"{Resumir(linha)}\" -> NOVA ALTERNATIVA \"{letra}\" ({motivo})");
            }
            else if (atual is not null)
            {
                atual.Texto = string.IsNullOrEmpty(atual.Texto) ? linha : $"{atual.Texto} {linha}";
                if (!sequenciaConfirmada)
                {
                    linhasDaTentativaNaoConfirmada.Add(linha);
                }

                logar?.Invoke($"Questão {numero}: linha \"{Resumir(linha)}\" -> CONTINUAÇÃO da alternativa \"{atual.Letra}\" ({motivo})");
            }
            else
            {
                enunciadoLinhas.Add(linha);
                logar?.Invoke($"Questão {numero}: linha \"{Resumir(linha)}\" -> ENUNCIADO ({motivo})");
            }
        }

        questao.Enunciado = MontarTextoComQuebras(enunciadoLinhas).Trim();
        questao.Alternativas = alternativas.OrderBy(a => a.Letra).ToList();

        if (questao.Enunciado.Length < 15)
        {
            questao.Alertas.Add("Texto aparentemente incompleto (enunciado muito curto ou não reconhecido).");
        }

        if (questao.Tipo == TipoQuestao.MultiplaEscolha && questao.Alternativas.Count < 2)
        {
            questao.Alertas.Add("Alternativas incompletas ou não reconhecidas (menos de 2 identificadas).");
        }

        return questao;
    }

    // Só pro log de diagnóstico — mantém as linhas legíveis quando ficam muito longas.
    private static string Resumir(string texto) => texto.Length > 80 ? texto[..80] + "..." : texto;

    // Junta linhas com "\n\n" perto de marcadores estruturais, e espaço simples
    // nas demais; só "\n\n" garante parágrafos separados de verdade no Markdown.
    private static string MontarTextoComQuebras(List<string> linhas)
    {
        if (linhas.Count == 0)
        {
            return "";
        }

        var sb = new System.Text.StringBuilder(linhas[0]);
        for (var i = 1; i < linhas.Count; i++)
        {
            var quebraDeParagrafo = EhLinhaEstrutural(linhas[i - 1]) || EhLinhaEstrutural(linhas[i]);
            sb.Append(quebraDeParagrafo ? "\n\n" : " ").Append(linhas[i]);
        }

        return sb.ToString();
    }

    private static bool EhLinhaEstrutural(string linha) =>
        LinhaComecaComRomanoOuTextoRegex.IsMatch(linha) || linha.Trim().Equals("PORQUE", StringComparison.OrdinalIgnoreCase);

    private static bool SugereImagemFaltando(QuestaoImportacaoEnade questao) =>
        questao.Imagens.Count == 0 &&
        PalavrasSugeremImagem.Any(p => questao.Enunciado.Contains(p, StringComparison.OrdinalIgnoreCase));

    // "Duplicidade possível" a nível de parser (mesmo Numero+Secao repetido no PDF),
    // diferente da checagem contra o banco feita por ImportadorProvaEnadeService.
    private static void MarcarDuplicidadeDentroDoLote(List<QuestaoImportacaoEnade> questoes)
    {
        foreach (var grupo in questoes.GroupBy(q => (q.Secao, q.NumeroOriginal)).Where(g => g.Count() > 1))
        {
            foreach (var q in grupo)
            {
                q.Alertas.Add($"Duplicidade possível: \"{grupo.Key.NumeroOriginal}\" aparece mais de uma vez nesta seção do PDF — confira se não é a mesma questão detectada duas vezes.");
            }
        }
    }

    // Status inicial só com o que o parser sabe; ImportadorProvaEnadeService
    // reclassifica depois de cruzar gabarito/duplicata-no-banco/Disciplina.
    private static void ClassificarStatusInicial(QuestaoImportacaoEnade questao)
    {
        var semMinimoNecessario =
            questao.Enunciado.Length < 15 ||
            (questao.Tipo == TipoQuestao.MultiplaEscolha && questao.Alternativas.Count < 2);

        questao.Status = semMinimoNecessario
            ? StatusPreImportacaoEnade.ComErro
            : (questao.Alertas.Count > 0 ? StatusPreImportacaoEnade.RequerRevisao : StatusPreImportacaoEnade.Validada);

        // Questão claramente quebrada não nasce marcada pra importar (o professor
        // ainda pode marcar de novo se quiser).
        if (questao.Status == StatusPreImportacaoEnade.ComErro)
        {
            questao.Selecionada = false;
        }
    }

    // --- Extração de texto em ordem de leitura (heurística de 2 colunas) ---
    // internal pois GabaritoEnadeParser reaproveita pro PDF de gabarito oficial.

    internal static string TextoEmOrdemDeLeitura(Page pagina)
    {
        List<Word> palavras;
        try
        {
            palavras = pagina.GetWords()?.ToList() ?? new List<Word>();
        }
        catch
        {
            return SeguroPageText(pagina);
        }

        // Palavras "fantasma" da página vizinha, fora da área visível, podem entrar na
        // extração crua — descartar pelo centro da palavra evita isso.
        palavras = palavras
            .Where(w => w.BoundingBox.Centroid.X >= 0 && w.BoundingBox.Centroid.X <= pagina.Width
                     && w.BoundingBox.Centroid.Y >= 0 && w.BoundingBox.Centroid.Y <= pagina.Height)
            .ToList();

        if (palavras.Count == 0)
        {
            return SeguroPageText(pagina);
        }

        var minX = palavras.Min(w => w.BoundingBox.Left);
        var maxX = palavras.Max(w => w.BoundingBox.Right);
        if (maxX - minX <= 0)
        {
            return SeguroPageText(pagina);
        }

        var gutterX = DetectarGutterDeColuna(palavras, minX, maxX);

        if (gutterX is null)
        {
            var ordenadas = palavras.OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);
            return AgruparEmLinhas(ordenadas);
        }

        // Confirma que o vão realmente separa DUAS colunas de texto: numa coluna
        // de verdade, NENHUMA linha física cruza o gutter (cada linha pertence
        // inteira a um lado). Se alguma linha tem palavras dos dois lados, o
        // "vão" era só um espaço largo no meio de uma frase comprida de coluna
        // única — tratar como coluna dividiria essa frase ao meio, jogando o
        // final pro fim do texto (depois de tudo da esquerda), fora de ordem.
        if (GutterCruzaAlgumaLinha(palavras, gutterX.Value))
        {
            var ordenadas = palavras.OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);
            return AgruparEmLinhas(ordenadas);
        }

        var esquerda = palavras.Where(w => w.BoundingBox.Centroid.X <= gutterX.Value)
            .OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);
        var direita = palavras.Where(w => w.BoundingBox.Centroid.X > gutterX.Value)
            .OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);

        return AgruparEmLinhas(esquerda) + "\n" + AgruparEmLinhas(direita);
    }

    // Mesmo agrupamento por linha física (tolerância vertical) usado em
    // AgruparEmLinhasComY, mas só pra checar se alguma linha tem palavras dos
    // dois lados do gutter candidato — nunca aceita group real de coluna que
    // faça isso.
    private static bool GutterCruzaAlgumaLinha(List<Word> palavras, double gutterX)
    {
        const double toleranciaVertical = 3.0;
        var ordenadas = palavras.OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);

        double? topDaLinha = null;
        var temEsquerda = false;
        var temDireita = false;

        foreach (var palavra in ordenadas)
        {
            if (topDaLinha is null || Math.Abs(topDaLinha.Value - palavra.BoundingBox.Top) > toleranciaVertical)
            {
                if (temEsquerda && temDireita)
                {
                    return true;
                }

                topDaLinha = palavra.BoundingBox.Top;
                temEsquerda = false;
                temDireita = false;
            }

            if (palavra.BoundingBox.Centroid.X <= gutterX)
            {
                temEsquerda = true;
            }
            else
            {
                temDireita = true;
            }
        }

        return temEsquerda && temDireita;
    }

    // Procura o maior "vão" sem palavra na faixa central (30%-70% da largura); acima de
    // 2% da largura vira divisor de coluna, senão a página é de coluna única.
    private static double? DetectarGutterDeColuna(List<Word> palavras, double minX, double maxX)
    {
        var largura = maxX - minX;
        var faixaMin = minX + largura * 0.3;
        var faixaMax = minX + largura * 0.7;

        var centros = palavras
            .Select(w => w.BoundingBox.Centroid.X)
            .Where(x => x >= faixaMin && x <= faixaMax)
            .OrderBy(x => x)
            .ToList();

        double maiorVao;
        double gutterX;

        if (centros.Count == 0)
        {
            maiorVao = faixaMax - faixaMin;
            gutterX = (faixaMin + faixaMax) / 2.0;
        }
        else
        {
            maiorVao = centros[0] - faixaMin;
            gutterX = (faixaMin + centros[0]) / 2.0;

            for (var i = 1; i < centros.Count; i++)
            {
                var vao = centros[i] - centros[i - 1];
                if (vao > maiorVao)
                {
                    maiorVao = vao;
                    gutterX = (centros[i - 1] + centros[i]) / 2.0;
                }
            }

            var vaoFinal = faixaMax - centros[^1];
            if (vaoFinal > maiorVao)
            {
                maiorVao = vaoFinal;
                gutterX = (centros[^1] + faixaMax) / 2.0;
            }
        }

        return maiorVao > largura * 0.02 ? gutterX : null;
    }

    private static string AgruparEmLinhas(IEnumerable<Word> palavrasOrdenadas)
    {
        return string.Join("\n", AgruparEmLinhasComY(palavrasOrdenadas).Select(l => l.Linha));
    }

    // Mesmo agrupamento de AgruparEmLinhas, mas preservando a coordenada Y média — usado
    // por DividirPaginaEmColunas pra parear linhas de duas colunas pela altura real.
    private static List<(string Linha, double Y)> AgruparEmLinhasComY(IEnumerable<Word> palavrasOrdenadas)
    {
        const double toleranciaVertical = 3.0;
        var linhas = new List<List<Word>>();

        foreach (var palavra in palavrasOrdenadas)
        {
            if (linhas.Count > 0 && Math.Abs(linhas[^1][0].BoundingBox.Top - palavra.BoundingBox.Top) <= toleranciaVertical)
            {
                linhas[^1].Add(palavra);
            }
            else
            {
                linhas.Add(new List<Word> { palavra });
            }
        }

        return linhas
            .Select(l => (MontarLinhaComEspacamentoAproximado(l), l.Average(w => w.BoundingBox.Top)))
            .ToList();
    }

    // Igual à divisão em duas colunas de TextoEmOrdemDeLeitura, mas devolve as colunas
    // SEPARADAS (com Y de cada linha) — usado por GabaritoEnadeParser pra parear "Item"/"Gabarito" pela altura real.
    internal static (List<(string Linha, double Y)> Esquerda, List<(string Linha, double Y)> Direita)? DividirPaginaEmColunas(Page pagina)
    {
        List<Word> palavras;
        try
        {
            palavras = pagina.GetWords()?.ToList() ?? new List<Word>();
        }
        catch
        {
            return null;
        }

        palavras = palavras
            .Where(w => w.BoundingBox.Centroid.X >= 0 && w.BoundingBox.Centroid.X <= pagina.Width
                     && w.BoundingBox.Centroid.Y >= 0 && w.BoundingBox.Centroid.Y <= pagina.Height)
            .ToList();

        if (palavras.Count == 0)
        {
            return null;
        }

        var minX = palavras.Min(w => w.BoundingBox.Left);
        var maxX = palavras.Max(w => w.BoundingBox.Right);
        if (maxX - minX <= 0)
        {
            return null;
        }

        var gutterX = DetectarGutterDeColuna(palavras, minX, maxX);
        if (gutterX is null)
        {
            return null;
        }

        var esquerda = palavras.Where(w => w.BoundingBox.Centroid.X <= gutterX.Value)
            .OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);
        var direita = palavras.Where(w => w.BoundingBox.Centroid.X > gutterX.Value)
            .OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);

        return (AgruparEmLinhasComY(esquerda), AgruparEmLinhasComY(direita));
    }

    // Reconstrói o espaçamento horizontal entre palavras de forma aproximada (em vez de
    // sempre um espaço só), pra preservar indentação de código-fonte; limitado a 12 espaços por vão.
    private static string MontarLinhaComEspacamentoAproximado(List<Word> palavrasDaLinha)
    {
        var ordenadas = palavrasDaLinha.OrderBy(w => w.BoundingBox.Left).ToList();
        if (ordenadas.Count == 0)
        {
            return "";
        }

        var sb = new System.Text.StringBuilder(ordenadas[0].Text);
        for (var i = 1; i < ordenadas.Count; i++)
        {
            var anterior = ordenadas[i - 1];
            var atual = ordenadas[i];
            var vao = atual.BoundingBox.Left - anterior.BoundingBox.Right;
            var larguraAnterior = anterior.BoundingBox.Right - anterior.BoundingBox.Left;
            var larguraCaractereAprox = anterior.Text.Length > 0 ? larguraAnterior / anterior.Text.Length : 0;

            // Limiar de 1.8x a largura de caractere: abaixo disso é sempre 1 espaço (senão
            // prosa normal vira falso-positivo de indentação); acima, trata como código/tabela.
            var espacos = larguraCaractereAprox > 0 && vao > larguraCaractereAprox * 1.8
                ? Math.Clamp((int)Math.Round(vao / larguraCaractereAprox), 2, 12)
                : 1;

            sb.Append(' ', espacos).Append(atual.Text);
        }

        return sb.ToString();
    }

    // --- Extração de imagens embutidas ---

    private static List<ImagemImportacaoEnade> ExtrairImagensDaPagina(Page pagina, Action<string>? logar = null)
    {
        var imagens = new List<ImagemImportacaoEnade>();

        IEnumerable<IPdfImage> imagensDaPagina;
        try
        {
            imagensDaPagina = pagina.GetImages().ToList();
        }
        catch (Exception ex)
        {
            logar?.Invoke($"[Imagens] Página {pagina.Number}: GetImages() lançou exceção ({ex.GetType().Name}: {ex.Message}) — nenhuma imagem extraída desta página.");
            return imagens;
        }

        var totalBrutas = imagensDaPagina.Count();
        logar?.Invoke($"[Imagens] Página {pagina.Number}: GetImages() retornou {totalBrutas} imagem(ns) bruta(s) do PDF.");

        var indice = 0;
        foreach (var imagem in imagensDaPagina)
        {
            indice++;
            try
            {
                // Tenta primeiro TryGetPng: cobre bitmaps com RawBytes em zlib/FlateDecode que o
                // sniff de magic number abaixo rejeitaria; TryGetPng não decodifica JPEG, daí o fallback abaixo.
                byte[]? pngDecodificado = null;
                bool tryGetPngOk;
                try
                {
                    tryGetPngOk = imagem.TryGetPng(out pngDecodificado);
                }
                catch (Exception ex)
                {
                    tryGetPngOk = false;
                    logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: TryGetPng() lançou exceção ({ex.GetType().Name}: {ex.Message}) — tentando RawBytes cru em seguida.");
                }

                if (tryGetPngOk && pngDecodificado is { Length: > 0 })
                {
                    imagens.Add(new ImagemImportacaoEnade
                    {
                        Conteudo = pngDecodificado,
                        ContentType = "image/png",
                        NomeArquivo = $"pagina-{pagina.Number}-imagem-{indice}.png",
                        PaginaOrigem = pagina.Number,
                    });
                    logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: aceita via TryGetPng (bitmap decodificado) como image/png ({pngDecodificado.Length} bytes).");
                    continue;
                }

                byte[]? bytes = null;
                try
                {
                    bytes = imagem.RawBytes?.ToArray();
                }
                catch (Exception ex)
                {
                    // Codificação interna não suportada nesta versão — pula em vez de
                    // gravar bytes inválidos como se fossem uma imagem de verdade.
                    logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: RawBytes lançou exceção ({ex.GetType().Name}) — pulada.");
                    continue;
                }

                if (bytes is null || bytes.Length == 0)
                {
                    logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: RawBytes vazio/nulo — pulada.");
                    continue;
                }

                var contentType = DetectarTipoDeImagem(bytes);
                if (contentType is null)
                {
                    // Nem TryGetPng nem o sniff deram conta (ex.: JPXDecode/JBIG2) — não
                    // incluída; fica só o alerta se o enunciado sugerir que precisava de imagem.
                    var assinatura = bytes.Length >= 4 ? Convert.ToHexString(bytes, 0, Math.Min(4, bytes.Length)) : "(vazio)";
                    logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: TryGetPng falhou e {bytes.Length} bytes de RawBytes com assinatura 0x{assinatura} não são JPEG nem PNG — pulada, formato não suportado.");
                    continue;
                }

                imagens.Add(new ImagemImportacaoEnade
                {
                    Conteudo = bytes,
                    ContentType = contentType,
                    NomeArquivo = $"pagina-{pagina.Number}-imagem-{indice}.{(contentType == "image/png" ? "png" : "jpg")}",
                    PaginaOrigem = pagina.Number,
                });
                logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: aceita como {contentType} ({bytes.Length} bytes).");
            }
            catch (Exception ex)
            {
                // Nunca deixa uma imagem problemática derrubar a extração da
                // página inteira.
                logar?.Invoke($"[Imagens] Página {pagina.Number}, imagem {indice}: exceção inesperada ({ex.GetType().Name}) — pulada.");
            }
        }

        return imagens;
    }

    // Sniff pelos primeiros bytes (magic numbers): JPEG (0xFFD8) é o caso mais comum de
    // imagem embutida em PDF; PNG também é possível. Outros formatos não são convertidos.
    private static string? DetectarTipoDeImagem(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        return null;
    }

    // Mesmo artefato de "conteúdo fantasma" de TextoEmOrdemDeLeitura, mas para imagens:
    // a mesma imagem (por hash) em mais de uma página fica só na primeira, nunca duplicada.
    private static void RemoverImagensFantasmaDuplicadas(Dictionary<int, List<ImagemImportacaoEnade>> imagensPorPagina)
    {
        var hashesVistos = new HashSet<string>();
        foreach (var numeroPagina in imagensPorPagina.Keys.OrderBy(n => n).ToList())
        {
            imagensPorPagina[numeroPagina] = imagensPorPagina[numeroPagina]
                .Where(img => hashesVistos.Add(Convert.ToBase64String(SHA256.HashData(img.Conteudo))))
                .ToList();
        }
    }

    private static List<ImagemImportacaoEnade> ImagensNoIntervalo(
        Dictionary<int, List<ImagemImportacaoEnade>> imagensPorPagina,
        int? paginaInicio, int? paginaFim,
        Dictionary<int, SecaoEnade?> secaoPorPagina, SecaoEnade secao)
    {
        if (paginaInicio is null)
        {
            return new List<ImagemImportacaoEnade>();
        }

        var fim = paginaFim ?? paginaInicio.Value;
        if (fim < paginaInicio.Value)
        {
            fim = paginaInicio.Value;
        }

        var resultado = new List<ImagemImportacaoEnade>();
        for (var pagina = paginaInicio.Value; pagina <= fim; pagina++)
        {
            // Só pega imagem de página que pertence à MESMA seção — evita uma imagem
            // da seção seguinte grudar na última questão da anterior.
            if (secaoPorPagina.GetValueOrDefault(pagina) != secao)
            {
                continue;
            }

            if (imagensPorPagina.TryGetValue(pagina, out var imagens))
            {
                resultado.AddRange(imagens);
            }
        }

        return resultado;
    }
}
