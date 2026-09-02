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
            .Include(p => p.ProvaQuestoes)
                .ThenInclude(pq => pq.Questao)
                    .ThenInclude(q => q!.Imagens)
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

            var questaoDto = new QuestaoExportDto
            {
                Enunciado = pq.Questao.Enunciado,
                Tipo = pq.Questao.TipoQuestao,
                Valor = pq.Valor,
                Explicacao = pq.Questao.Explicacao,
                Imagens = pq.Questao.Imagens
                    .OrderBy(im => im.Ordem)
                    .Select(im => new ImagemExportDto
                    {
                        Conteudo = im.Conteudo,
                        ContentType = im.ContentType,
                        Legenda = im.Legenda,
                        TextoAlternativo = im.TextoAlternativo,
                        Alinhamento = im.Alinhamento,
                        LarguraPercentual = im.LarguraPercentual,
                    })
                    .ToList(),
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
                // Sorteia a ordem da coluna B aqui (não na exportação de variações,
                // que gera sua PRÓPRIA ordem por versão) — cobre a exportação simples,
                // que passa a prova direto sem passar pelo embaralhador de versões.
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

            // Fica por último de propósito: roda depois da substituição de marcadores
            // de Lacunas, então opera sempre sobre o texto final que será impresso.
            var blocos = await MarkdownConversor.ConverterAsync(questaoDto.Enunciado);
            if (blocos.Count > 0)
            {
                questaoDto.EnunciadoBlocos = blocos;
            }

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

    // Troca cada sequência de 3+ underscores no enunciado por um marcador numerado
    // ("_____(1)_____"), na ordem em que aparecem — assim o aluno (e o gabarito)
    // conseguem referenciar "a lacuna 1", "a lacuna 2" etc. sem ambiguidade.
    //
    // Os underscores saem ESCAPADOS (\_) de propósito: esse texto passa por um
    // conversor de Markdown logo em seguida (aqui ou em quem chamar isso — ver
    // QuestaoList.razor, que reusa este método pro modal de prévia), e 5
    // underscores seguidos sem escape seriam lidos como ênfase (negrito/itálico)
    // em vez de aparecerem como a linha de preenchimento da lacuna.
    //
    // Público (não só interno ao loader) porque o modal de "prévia da questão"
    // em QuestaoList.razor precisa do MESMO texto substituído pra mostrar a
    // lacuna exatamente como vai sair na prova.
    public static string SubstituirMarcadoresDeLacuna(string enunciado)
    {
        var contador = 0;
        return Regex.Replace(enunciado, "_{3,}", _ => $"\\_\\_\\_\\_\\_({++contador})\\_\\_\\_\\_\\_");
    }
}
