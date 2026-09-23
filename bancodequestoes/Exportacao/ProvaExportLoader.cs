using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BancoQuestoes.Exportacao;

public static class ProvaExportLoader
{
    public static async Task<ProvaExportDto?> CarregarAsync(ApplicationDbContext db, int provaId)
    {
        var prova = await db.Provas
            .Include(p => p.Disciplina)
            .Include(p => p.Curso)
                .ThenInclude(c => c!.Instituicao)
            .Include(p => p.Turma)
            .Include(p => p.CriadoPor)
            // Escopo: só o necessário pro cabeçalho impresso (ver ProvaExportDto.EscopoRotulo).
            .Include(p => p.ProvaDisciplinas)
                .ThenInclude(pd => pd.Disciplina)
            .Include(p => p.MatrizReferencia)
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.Imagens)
            // Várias coleções na mesma consulta — ver comentário equivalente em ProvaService.ListarAsync.
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == provaId);

        if (prova is null)
        {
            return null;
        }

        var dto = new ProvaExportDto
        {
            Titulo = prova.Titulo,
            Disciplina = prova.Disciplina?.Nome ?? "",
            Professor = prova.CriadoPor?.NomeCompleto,
            Curso = prova.Curso?.Nome,
            Tipo = prova.Tipo,
            TipoEscopo = prova.TipoEscopo,
            DisciplinasMultidisciplinar = prova.ProvaDisciplinas
                .Select(pd => pd.Disciplina?.Nome)
                .Where(nome => nome is not null)
                .Select(nome => nome!)
                .OrderBy(nome => nome)
                .ToList(),
            MatrizTipo = prova.MatrizReferencia?.Tipo,
            Turma = prova.Turma?.Rotulo,
            DataAplicacao = prova.DataAplicacao,
            TempoEstimadoMinutos = prova.TempoEstimadoMinutos,
            Instituicao = prova.Curso?.Instituicao is null
                ? null
                : new InstituicaoExportDto
                {
                    Nome = prova.Curso.Instituicao.Nome,
                    Endereco = prova.Curso.Instituicao.Endereco,
                    Cidade = prova.Curso.Instituicao.Cidade,
                    Telefone = prova.Curso.Instituicao.Telefone,
                    Site = prova.Curso.Instituicao.Site,
                    LogoConteudo = prova.Curso.Instituicao.LogoConteudo,
                    LogoContentType = prova.Curso.Instituicao.LogoContentType,
                    Instrucoes = (prova.Curso.Instituicao.Instrucoes ?? "")
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList(),
                },
        };

        foreach (var pq in prova.ProvaQuestoes.OrderBy(pq => pq.Ordem))
        {
            if (pq.Questao is null)
            {
                continue;
            }

            // Capturado numa variável local pra função local ResolverImagemDoEnunciado
            // abaixo não depender do analisador de nulidade propagar o "is null" acima.
            var questaoEntidade = pq.Questao;

            var questaoDto = new QuestaoExportDto
            {
                Enunciado = pq.Questao.Enunciado,
                Tipo = pq.Questao.TipoQuestao,
                Valor = pq.Valor,
                Origem = pq.Questao.Origem,
                Ano = pq.Questao.Ano,
                Explicacao = pq.Questao.Explicacao,
                // Imagens é preenchido mais abaixo, depois de converter o Enunciado
                // (precisa saber quais já foram desenhadas embutidas — ver idsRenderizadosInline).
            };

            if (pq.Questao is QuestaoMultiplaEscolha me)
            {
                await db.Entry(me).Collection(x => x.Alternativas).LoadAsync();
                questaoDto.Alternativas = me.Alternativas
                    .OrderBy(a => a.Letra)
                    .Select(a => new AlternativaExportDto { Texto = a.Texto, Correta = a.Letra == me.RespostaCorreta })
                    .ToList();
            }
            else if (pq.Questao is QuestaoCertoErrado ce)
            {
                questaoDto.RespostaCertoErrado = ce.RespostaCorreta;
            }
            else if (pq.Questao is QuestaoDiscursiva discursiva)
            {
                questaoDto.RespostaEsperadaDiscursiva = discursiva.RespostaEsperada;
                questaoDto.CriterioAvaliacaoDiscursiva = discursiva.CriterioAvaliacao;
            }
            else if (pq.Questao is QuestaoRespostaBreve rb)
            {
                questaoDto.RespostaBreveEsperada = rb.RespostaEsperada;
            }
            else if (pq.Questao is QuestaoNumerica numerica)
            {
                questaoDto.Numerica = new NumericaExportDto
                {
                    RespostaEsperada = numerica.RespostaEsperada,
                    Tolerancia = numerica.Tolerancia,
                };
            }
            else if (pq.Questao is QuestaoAssociacao associacao)
            {
                await db.Entry(associacao).Collection(a => a.Pares).LoadAsync();
                var pares = associacao.Pares
                    .OrderBy(p => p.Ordem)
                    .Select(p => new ParAssociacaoExportDto { Termo = p.Termo, Correspondente = p.Correspondente })
                    .ToList();
                questaoDto.Pares = pares;
                // Sorteia a ordem da coluna B aqui pra cobrir a exportação simples
                // (a de variações gera sua própria ordem por versão).
                questaoDto.OrdemCorrespondentes = Enumerable.Range(0, pares.Count).OrderBy(_ => Random.Shared.Next()).ToList();
            }
            else if (pq.Questao is QuestaoLacunas lacunas)
            {
                await db.Entry(lacunas).Collection(l => l.Lacunas).LoadAsync();
                questaoDto.LacunasRespostas = lacunas.Lacunas
                    .OrderBy(l => l.Ordem)
                    .Select(l => l.RespostaEsperada)
                    .ToList();
                questaoDto.Enunciado = SubstituirMarcadoresDeLacuna(questaoDto.Enunciado);
            }

            // Resolve referências "imagem:existente:{id}" do EnunciadoEditor pra desenhar a
            // imagem embutida; idsRenderizadosInline evita desenhar de novo no bloco final.
            var idsRenderizadosInline = new HashSet<int>();
            Task<ImagemExportDto?> ResolverImagemDoEnunciado(string url)
            {
                const string prefixo = "imagem:existente:";
                if (!url.StartsWith(prefixo, StringComparison.Ordinal) || !int.TryParse(url.AsSpan(prefixo.Length), out var imagemId))
                {
                    return Task.FromResult<ImagemExportDto?>(null);
                }
                var imagem = questaoEntidade.Imagens.FirstOrDefault(im => im.Id == imagemId);
                if (imagem is null)
                {
                    return Task.FromResult<ImagemExportDto?>(null);
                }
                idsRenderizadosInline.Add(imagemId);
                return Task.FromResult<ImagemExportDto?>(new ImagemExportDto
                {
                    Conteudo = imagem.Conteudo,
                    ContentType = imagem.ContentType,
                    Legenda = imagem.Legenda,
                    TextoAlternativo = imagem.TextoAlternativo,
                    Alinhamento = imagem.Alinhamento,
                    LarguraPercentual = imagem.LarguraPercentual,
                });
            }

            // Fica por último de propósito: roda depois da substituição de marcadores
            // de Lacunas, então opera sempre sobre o texto final que será impresso.
            var blocos = await MarkdownConversor.ConverterAsync(questaoDto.Enunciado, ResolverImagemDoEnunciado);
            if (blocos.Count > 0)
            {
                questaoDto.EnunciadoBlocos = blocos;
            }

            // Prova.MostrarValorNoEnunciado: valor entra como último trecho de texto,
            // colado ao último bloco (ou em bloco novo se terminar em algo não-textual).
            if (prova.MostrarValorNoEnunciado && pq.Valor is { } valorQuestao)
            {
                var textoValor = $" ({valorQuestao:0.##} pt{(valorQuestao == 1 ? "" : "s")})";
                var blocoTextualFinal = blocos.LastOrDefault(b =>
                    b.Tipo is TipoBlocoMarkdown.Paragrafo or TipoBlocoMarkdown.ItemListaComMarcador or TipoBlocoMarkdown.ItemListaNumerada);
                if (blocoTextualFinal is not null)
                {
                    blocoTextualFinal.Trechos.Add(new TrechoTexto { Texto = textoValor, Italico = true });
                }
                else
                {
                    blocos.Add(new BlocoMarkdown
                    {
                        Tipo = TipoBlocoMarkdown.Paragrafo,
                        Trechos = new List<TrechoTexto> { new() { Texto = textoValor.TrimStart(), Italico = true } },
                    });
                }
                questaoDto.EnunciadoBlocos = blocos;
            }

            // Só as imagens que não foram desenhadas embutidas acima vão pro bloco
            // final da questão — evita a mesma imagem aparecer duas vezes.
            questaoDto.Imagens = questaoEntidade.Imagens
                .OrderBy(im => im.Ordem)
                .Where(im => !idsRenderizadosInline.Contains(im.Id))
                .Select(im => new ImagemExportDto
                {
                    Conteudo = im.Conteudo,
                    ContentType = im.ContentType,
                    Legenda = im.Legenda,
                    TextoAlternativo = im.TextoAlternativo,
                    Alinhamento = im.Alinhamento,
                    LarguraPercentual = im.LarguraPercentual,
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(questaoDto.Explicacao))
            {
                var explicacaoBlocos = await MarkdownConversor.ConverterAsync(questaoDto.Explicacao);
                if (explicacaoBlocos.Count > 0)
                {
                    questaoDto.ExplicacaoBlocos = explicacaoBlocos;
                }
            }

            dto.Questoes.Add(questaoDto);
        }

        return dto;
    }

    // Troca sequências de 3+ underscores por marcador numerado, escapado pra não virar
    // ênfase no Markdown; público pois QuestaoList.razor reusa no modal de prévia.
    public static string SubstituirMarcadoresDeLacuna(string enunciado)
    {
        var contador = 0;
        return Regex.Replace(enunciado, "_{3,}", _ => $"\\_\\_\\_\\_\\_({++contador})\\_\\_\\_\\_\\_");
    }
}
