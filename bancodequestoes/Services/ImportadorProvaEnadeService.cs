using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BancoQuestoes.Services;

// Orquestração do importador ENADE — único ponto que toca banco. ProcessarAsync
// nunca cria Questao; ImportarSelecionadasAsync é quem persiste, por questão.
public interface IImportadorProvaEnadeService
{
    // pdfPadraoResposta: terceiro documento oficial (padrão de resposta das
    // discursivas D1/D2), opcional como o gabarito.
    Task<ResultadoImportacaoEnade> ProcessarAsync(byte[] pdfProva, byte[]? pdfGabarito, byte[]? pdfPadraoResposta, int ano, int areaCursoId);
    Task<ResultadoImportacaoDefinitivaEnade> ImportarSelecionadasAsync(ResultadoImportacaoEnade resultado, string? criadoPorId, int? minhaInstituicaoId);
}

public class ImportadorProvaEnadeService(
    ApplicationDbContext db, QuestaoService questaoService, IPaginaPdfRenderizador renderizador,
    ILogger<ImportadorProvaEnadeService> logger) : IImportadorProvaEnadeService
{
    public async Task<ResultadoImportacaoEnade> ProcessarAsync(byte[] pdfProva, byte[]? pdfGabarito, byte[]? pdfPadraoResposta, int ano, int areaCursoId)
    {
        if (ano < 1990 || ano > DateTime.UtcNow.Year + 1)
        {
            throw new OperacaoInvalidaException("Informe um ano de prova válido.");
        }

        var area = await db.AreasCurso.FirstOrDefaultAsync(a => a.Id == areaCursoId)
            ?? throw new OperacaoInvalidaException("Selecione uma Área de Curso válida.");

        // Log de diagnóstico verboso pra investigar bugs de parsing — aparece direto no console em dev.
        var resultado = EnadeProvaParser.Extrair(pdfProva,
            linha => logger.LogInformation("[EnadeParser] {Linha}", linha));
        resultado.Ano = ano;
        resultado.AreaCursoId = areaCursoId;
        resultado.NomeAreaCurso = area.Nome;

        if (resultado.Questoes.Count == 0)
        {
            // Já tem AlertasGerais explicando o motivo — nada mais a fazer.
            return resultado;
        }

        // Gabarito oficial (opcional) — casa por (Seção, NumeroOriginal)
        // contra o que o parser da prova já extraiu, nunca inferido.
        GabaritoEnadeParser.ResultadoGabarito? gabarito = null;
        if (pdfGabarito is { Length: > 0 })
        {
            gabarito = GabaritoEnadeParser.Extrair(pdfGabarito,
                linha => logger.LogInformation("[Gabarito] {Linha}", linha));
            resultado.AlertasGerais.AddRange(gabarito.Alertas);
            resultado.TinhaGabarito = true;
        }

        // Padrão de resposta discursivo oficial (opcional) — casa por
        // NumeroOriginal ("D1"/"D2"), nunca inferido do enunciado.
        PadraoRespostaEnadeParser.ResultadoPadraoResposta? padraoResposta = null;
        if (pdfPadraoResposta is { Length: > 0 })
        {
            padraoResposta = PadraoRespostaEnadeParser.Extrair(pdfPadraoResposta);
            resultado.AlertasGerais.AddRange(padraoResposta.Alertas);
            resultado.TinhaPadraoResposta = true;
        }

        // Disciplina/Assunto "Formação Geral" são seedados no startup (ver
        // DbSeeder) — busca por Codigo, nunca por Nome.
        var disciplinaFormacaoGeral = await db.Disciplinas
            .Include(d => d.Assuntos)
            .FirstOrDefaultAsync(d => d.Codigo == Disciplina.CodigoFormacaoGeral);
        var assuntoFormacaoGeral = disciplinaFormacaoGeral?.Assuntos.FirstOrDefault(a => a.Nome == "Conhecimentos Gerais");

        if (disciplinaFormacaoGeral is null || assuntoFormacaoGeral is null)
        {
            resultado.AlertasGerais.Add(
                "A disciplina padrão \"Formação Geral\" ainda não existe neste banco (deveria ter sido criada automaticamente na inicialização do sistema) — " +
                "questões de Formação Geral não podem ser pré-preenchidas e vão precisar de revisão manual da Disciplina.");
        }

        foreach (var questao in resultado.Questoes)
        {
            questao.Ano = ano;

            if (questao.Secao == SecaoEnade.FormacaoGeral)
            {
                questao.AreaCursoId = null;
                if (disciplinaFormacaoGeral is not null && assuntoFormacaoGeral is not null)
                {
                    questao.DisciplinaId = disciplinaFormacaoGeral.Id;
                    questao.AssuntoId = assuntoFormacaoGeral.Id;
                }
            }
            else if (questao.Secao == SecaoEnade.ComponenteEspecifico)
            {
                questao.AreaCursoId = areaCursoId;
                // Disciplina específica nunca é adivinhada — fica em 0 até o
                // professor decidir na revisão, sempre como alerta.
                questao.Alertas.Add("Escolha a Disciplina (e o Assunto) desta questão antes de importar.");
            }
            else
            {
                questao.Alertas.Add("Seção (Formação Geral / Componente Específico) não identificada — escolha manualmente.");
            }

            if (gabarito is not null && questao.Tipo == TipoQuestao.MultiplaEscolha)
            {
                AplicarGabarito(questao, gabarito);
            }
            else if (questao.Tipo == TipoQuestao.MultiplaEscolha)
            {
                questao.Alertas.Add("Gabarito pendente — nenhum arquivo de gabarito oficial foi informado.");
            }

            if (questao.Tipo == TipoQuestao.Discursiva)
            {
                AplicarPadraoResposta(questao, padraoResposta);
            }

            await MarcarDuplicataNoBancoAsync(questao);

            ClassificarStatusFinal(questao);
        }

        // Snapshot de página de origem (modelo híbrido) gerado só DEPOIS de
        // tudo decidido — nunca influencia classificação, é só referência visual.
        AplicarRepresentacoesOriginais(resultado, pdfProva, pdfPadraoResposta);

        return resultado;
    }

    // Popula o snapshot de página de cada questão, sem substituir o conteúdo
    // estruturado. Falha ao renderizar nunca impede a importação.
    private void AplicarRepresentacoesOriginais(ResultadoImportacaoEnade resultado, byte[] pdfProva, byte[]? pdfPadraoResposta)
    {
        var paginasDaProva = resultado.Questoes
            .Where(q => q.PaginaOrigem is not null)
            .SelectMany(q => PaginasDoIntervalo(q.PaginaOrigem!.Value, q.PaginaFim ?? q.PaginaOrigem!.Value))
            .Distinct();

        IReadOnlyDictionary<int, byte[]> renderizadasDaProva;
        try
        {
            renderizadasDaProva = renderizador.RenderizarPaginas(pdfProva, paginasDaProva);
        }
        catch
        {
            renderizadasDaProva = new Dictionary<int, byte[]>();
        }

        IReadOnlyDictionary<int, byte[]> renderizadasDoPadrao = new Dictionary<int, byte[]>();
        if (pdfPadraoResposta is { Length: > 0 })
        {
            var paginasDoPadrao = resultado.Questoes
                .Where(q => q.PaginaOrigemPadraoResposta is not null)
                .Select(q => q.PaginaOrigemPadraoResposta!.Value)
                .Distinct();

            try
            {
                renderizadasDoPadrao = renderizador.RenderizarPaginas(pdfPadraoResposta, paginasDoPadrao);
            }
            catch
            {
                renderizadasDoPadrao = new Dictionary<int, byte[]>();
            }
        }

        foreach (var questao in resultado.Questoes)
        {
            if (questao.PaginaOrigem is int inicio)
            {
                foreach (var pagina in PaginasDoIntervalo(inicio, questao.PaginaFim ?? inicio))
                {
                    questao.RepresentacoesOriginais.Add(new RepresentacaoOriginalEnade
                    {
                        Pagina = pagina,
                        Conteudo = renderizadasDaProva.TryGetValue(pagina, out var png) ? png : null,
                    });
                }
            }

            if (questao.PaginaOrigemPadraoResposta is int paginaPadrao)
            {
                questao.RepresentacoesOriginaisPadraoResposta.Add(new RepresentacaoOriginalEnade
                {
                    Pagina = paginaPadrao,
                    Conteudo = renderizadasDoPadrao.TryGetValue(paginaPadrao, out var pngPadrao) ? pngPadrao : null,
                });
            }
        }
    }

    private static IEnumerable<int> PaginasDoIntervalo(int inicio, int fim)
    {
        var fimEfetivo = fim < inicio ? inicio : fim;
        for (var pagina = inicio; pagina <= fimEfetivo; pagina++)
        {
            yield return pagina;
        }
    }

    private static void AplicarGabarito(QuestaoImportacaoEnade questao, GabaritoEnadeParser.ResultadoGabarito gabarito)
    {
        if (!gabarito.Respostas.TryGetValue(questao.NumeroOriginal, out var letra))
        {
            questao.Alertas.Add("Gabarito pendente — esta questão não foi encontrada no arquivo de gabarito informado.");
            return;
        }

        var indice = questao.Alternativas.FindIndex(a => a.Letra == letra);
        if (indice < 0)
        {
            questao.Alertas.Add($"O gabarito indica a letra \"{letra}\", mas essa alternativa não foi reconhecida no enunciado — confira manualmente.");
            return;
        }

        questao.RespostaCorretaIndex = indice;
    }

    // Cruza a discursiva com o padrão oficial — nunca inventa a resposta,
    // só copia o texto já achado. Sem padrão, mantém o alerta manual.
    private static void AplicarPadraoResposta(QuestaoImportacaoEnade questao, PadraoRespostaEnadeParser.ResultadoPadraoResposta? padraoResposta)
    {
        if (padraoResposta is null)
        {
            questao.Alertas.Add("Questão discursiva — nenhum arquivo de padrão de resposta foi informado; preencha a resposta esperada/critério de correção antes de importar.");
            return;
        }

        if (!padraoResposta.Respostas.TryGetValue(questao.NumeroOriginal, out var padrao))
        {
            questao.Alertas.Add($"Padrão oficial não encontrado para a questão \"{questao.NumeroOriginal}\" no arquivo de padrão de resposta informado — preencha manualmente antes de importar.");
            return;
        }

        questao.RespostaEsperadaDiscursiva = padrao.TextoCompleto;
        questao.ItensDiscursivos = padrao.Itens;
        questao.PaginaOrigemPadraoResposta = padrao.PaginaOrigem;
        questao.CriterioAvaliacaoDiscursiva = MontarCriterioAvaliacao(padrao.Itens);

        // Com padrão oficial encontrado e resposta preenchida, o alerta de
        // "preencha a resposta esperada" já não se aplica mais.
    }

    // Rubrica legível a partir dos subitens do padrão oficial — só reorganiza,
    // nunca inventa texto novo. Null quando não há subitens estruturados.
    private static string? MontarCriterioAvaliacao(List<ItemDiscursivoImportacaoEnade> itens)
    {
        if (itens.Count == 0)
        {
            return null;
        }

        var linhas = itens.Select(item =>
        {
            var pontuacao = item.Pontuacao is decimal p ? $" ({p.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} pontos)" : "";
            return $"{item.Codigo}){pontuacao} {item.RespostaPadrao}";
        });

        return string.Join("\n\n", linhas);
    }

    // Duplicidade contra o banco via QuestaoEnadeChaveNatural — só sinaliza, nunca bloqueia.
    private async Task MarcarDuplicataNoBancoAsync(QuestaoImportacaoEnade questao)
    {
        if (questao.Secao is not { } secao || string.IsNullOrWhiteSpace(questao.NumeroOriginal))
        {
            return;
        }

        var duplicata = await QuestaoEnadeChaveNatural.EncontrarPossivelDuplicataAsync(
            db, questao.Ano, secao, questao.NumeroOriginal,
            areaCursoId: secao == SecaoEnade.ComponenteEspecifico ? questao.AreaCursoId : null);

        if (duplicata is not null)
        {
            questao.PossivelDuplicata = true;
            questao.QuestaoDuplicadaId = duplicata.Id;
            questao.Alertas.Add($"Possível questão já existente no banco (Questão #{duplicata.Id}) — reveja antes de importar de novo.");
        }
    }

    private static void ClassificarStatusFinal(QuestaoImportacaoEnade questao)
    {
        if (questao.Status == StatusPreImportacaoEnade.ComErro)
        {
            return; // já nasceu quebrada no parser — nada aqui muda isso.
        }

        questao.Status = questao.Alertas.Count > 0
            ? StatusPreImportacaoEnade.RequerRevisao
            : StatusPreImportacaoEnade.Validada;
    }

    // Cada questão é uma chamada independente a QuestaoService.CriarAsync.
    // Sem transação de lote de propósito: o relatório reporta parcialmente o que importou/falhou.
    public async Task<ResultadoImportacaoDefinitivaEnade> ImportarSelecionadasAsync(
        ResultadoImportacaoEnade resultado, string? criadoPorId, int? minhaInstituicaoId)
    {
        var relatorio = new ResultadoImportacaoDefinitivaEnade();

        foreach (var questao in resultado.Questoes)
        {
            if (!questao.Selecionada)
            {
                questao.Status = StatusPreImportacaoEnade.Ignorada;
                relatorio.Ignoradas.Add(questao);
                continue;
            }

            try
            {
                var modelo = ConverterParaQuestaoInput(questao);
                var imagensNovas = questao.Imagens.Select(img => new PendenteImagem
                {
                    NomeArquivo = img.NomeArquivo,
                    ContentType = img.ContentType,
                    Conteudo = img.Conteudo,
                }).ToList();

                await questaoService.CriarAsync(modelo, imagensNovas, criadoPorId, minhaInstituicaoId);

                questao.Status = StatusPreImportacaoEnade.Importada;
                relatorio.Importadas.Add(questao);
            }
            catch (OperacaoInvalidaException ex)
            {
                relatorio.ComErro.Add((questao, ex.Message));
            }
            catch (Exception ex)
            {
                // Isola a falha desta questão sem derrubar o restante do
                // lote — a mensagem completa ainda vai pro relatório.
                relatorio.ComErro.Add((questao, ex.Message));
            }
        }

        return relatorio;
    }

    // Mapeamento direto QuestaoImportacaoEnade -> QuestaoInput — nenhuma
    // regra nova aqui, quem valida de verdade é QuestaoService.CriarAsync.
    private static QuestaoInput ConverterParaQuestaoInput(QuestaoImportacaoEnade questao)
    {
        var modelo = new QuestaoInput
        {
            TipoQuestao = questao.Tipo,
            AssuntoId = questao.AssuntoId,
            Dificuldade = questao.Dificuldade,
            Visibilidade = VisibilidadeQuestao.Privada,
            Bloom = questao.Bloom,
            Origem = OrigemQuestao.Enade,
            Ano = questao.Ano,
            SecaoEnade = questao.Secao,
            NumeroOriginal = questao.NumeroOriginal,
            Enunciado = questao.Enunciado,
            AreaCursoIds = questao.AreaCursoId is int areaId ? new List<int> { areaId } : new List<int>(),
            ItemMatrizIds = questao.ItemMatrizIds.ToList(),
        };

        if (questao.Tipo == TipoQuestao.MultiplaEscolha)
        {
            if (questao.RespostaCorretaIndex is not int indiceCorreta)
            {
                throw new OperacaoInvalidaException("Gabarito pendente — selecione a resposta correta antes de importar esta questão.");
            }

            modelo.Alternativas = questao.Alternativas
                .Select(a => new AlternativaInput { Texto = a.Texto })
                .ToList();
            modelo.RespostaCorretaIndex = indiceCorreta;
        }
        else if (questao.Tipo == TipoQuestao.Discursiva)
        {
            if (string.IsNullOrWhiteSpace(questao.RespostaEsperadaDiscursiva))
            {
                throw new OperacaoInvalidaException("Preencha a resposta esperada/critério de correção antes de importar esta questão discursiva.");
            }

            modelo.RespostaEsperada = questao.RespostaEsperadaDiscursiva;
            modelo.CriterioAvaliacao = questao.CriterioAvaliacaoDiscursiva;
        }
        else
        {
            throw new OperacaoInvalidaException($"Tipo de questão \"{questao.Tipo}\" não suportado pelo importador de provas ENADE.");
        }

        return modelo;
    }
}
