using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Wrapper fino sobre BancoQuestoes.Importacao (GiftParser/AikenParser) —
// o parsing em si (texto -> ResultadoImportacao) já é lógica estável e
// autocontida, então não é reescrito aqui, só invocado. O que este Service
// acrescenta é a parte que QuestaoImportar.razor fazia direto no DbContext:
// converter as QuestaoImportada selecionadas em entidades Questao e salvar.
//
// A exportação GIFT/Aiken (QuestaoList.razor, botões "Exportar") já é só
// GiftExporter.Gerar/AikenExporter.Gerar (estático, sem banco) chamados
// direto da tela — não precisa de wrapper aqui.
public class ImportacaoService(ApplicationDbContext db)
{
    public ResultadoImportacao Analisar(string texto, string formato) =>
        formato == "Gift" ? GiftParser.Parse(texto) : AikenParser.Parse(texto);

    public async Task<int> ImportarAsync(ResultadoImportacao resultado, int assuntoId, Dificuldade dificuldade, string? criadoPorId)
    {
        if (assuntoId == 0)
        {
            throw new OperacaoInvalidaException("Selecione o assunto de destino.");
        }

        var importadas = 0;
        foreach (var q in resultado.Questoes.Where(q => q.Selecionada))
        {
            Questao nova = q.Tipo switch
            {
                TipoQuestao.MultiplaEscolha => new QuestaoMultiplaEscolha
                {
                    Enunciado = q.Enunciado,
                    RespostaCorreta = (char)('A' + Math.Max(0, q.Alternativas.FindIndex(a => a.Correta))),
                    Alternativas = q.Alternativas
                        .Select((a, i) => new AlternativaQuestao { Letra = (char)('A' + i), Texto = a.Texto })
                        .ToList(),
                },
                TipoQuestao.CertoErrado => new QuestaoCertoErrado
                {
                    Enunciado = q.Enunciado,
                    RespostaCorreta = q.RespostaCertoErrado ?? true,
                },
                TipoQuestao.RespostaBreve => new QuestaoRespostaBreve
                {
                    Enunciado = q.Enunciado,
                    RespostaEsperada = q.RespostaBreveEsperada ?? "",
                },
                TipoQuestao.Numerica => new QuestaoNumerica
                {
                    Enunciado = q.Enunciado,
                    RespostaEsperada = q.NumericaEsperada ?? 0,
                    Tolerancia = q.NumericaTolerancia ?? 0,
                },
                TipoQuestao.Associacao => new QuestaoAssociacao
                {
                    Enunciado = q.Enunciado,
                    Pares = q.Pares
                        .Select((p, i) => new ParAssociacao { Termo = p.Termo, Correspondente = p.Correspondente, Ordem = i })
                        .ToList(),
                },
                _ => throw new InvalidOperationException("Tipo de questão importada inválido."),
            };

            nova.AssuntoId = assuntoId;
            nova.Dificuldade = dificuldade;
            nova.TipoQuestao = q.Tipo;
            nova.CriadoPorId = criadoPorId;
            nova.Origem = OrigemQuestao.Importada;

            db.Questoes.Add(nova);
            importadas++;
        }

        await db.SaveChangesAsync();
        return importadas;
    }
}
