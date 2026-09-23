using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Tipos de questão suportados na correção automática da prova online v1 — Discursiva,
// Associação e Resposta Breve ficam pra uma evolução futura (exigem correção manual ou uma
// comparação mais elaborada). Usado por AplicacaoProvaService (bloqueia a aplicação de provas
// com tipos não suportados) e por RespostaProvaOnlineService (corrige cada resposta).
public static class TiposAutoCorrigiveis
{
    public static readonly TipoQuestao[] Tipos =
    {
        TipoQuestao.MultiplaEscolha,
        TipoQuestao.CertoErrado,
        TipoQuestao.Numerica,
        TipoQuestao.Lacunas,
    };
}
