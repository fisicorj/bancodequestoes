using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Recálculo LOCAL (sem banco) de Alertas/Status depois que o professor edita uma
// questão na revisão, sem reprocessar o PDF; preserva os alertas de duplicidade, que só fazem sentido vindos de fora.
public static class RevisaoEnadeHelper
{
    public static void Revalidar(QuestaoImportacaoEnade questao)
    {
        var preservados = questao.Alertas
            .Where(a => a.Contains("Duplicidade possível", StringComparison.OrdinalIgnoreCase)
                     || a.Contains("Possível questão já existente", StringComparison.OrdinalIgnoreCase))
            .ToList();

        questao.Alertas = preservados;

        var enunciadoCurto = questao.Enunciado.Trim().Length < 15;
        if (enunciadoCurto)
        {
            questao.Alertas.Add("Texto aparentemente incompleto (enunciado muito curto).");
        }

        if (questao.Secao is null)
        {
            questao.Alertas.Add("Seção (Formação Geral / Componente Específico) não identificada — escolha manualmente.");
        }
        else if (questao.Secao == SecaoEnade.ComponenteEspecifico && questao.AssuntoId == 0)
        {
            questao.Alertas.Add("Escolha a Disciplina (e o Assunto) desta questão antes de importar.");
        }

        var alternativasPreenchidas = questao.Alternativas.Count(a => !string.IsNullOrWhiteSpace(a.Texto));
        var alternativasIncompletas = questao.Tipo == TipoQuestao.MultiplaEscolha && alternativasPreenchidas < 2;

        if (questao.Tipo == TipoQuestao.MultiplaEscolha)
        {
            if (alternativasIncompletas)
            {
                questao.Alertas.Add("Alternativas incompletas (menos de 2 preenchidas).");
            }

            if (questao.RespostaCorretaIndex is null)
            {
                questao.Alertas.Add("Gabarito pendente — selecione a resposta correta.");
            }
        }
        else if (questao.Tipo == TipoQuestao.Discursiva && string.IsNullOrWhiteSpace(questao.RespostaEsperadaDiscursiva))
        {
            // ImportadorProvaEnadeService.AplicarPadraoResposta preenche
            // RespostaEsperadaDiscursiva ANTES da revalidação, então o alerta só aparece quando realmente falta.
            questao.Alertas.Add("Questão discursiva — preencha a resposta esperada/critério de correção.");
        }

        var semMinimoNecessario = enunciadoCurto || alternativasIncompletas;

        questao.Status = semMinimoNecessario
            ? StatusPreImportacaoEnade.ComErro
            : (questao.Alertas.Count > 0 ? StatusPreImportacaoEnade.RequerRevisao : StatusPreImportacaoEnade.Validada);
    }
}
