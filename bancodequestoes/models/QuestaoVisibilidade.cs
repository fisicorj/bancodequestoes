using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Filtro central de "quem pode ENXERGAR essa questão" (privada do dono,
// institucional entre professores da mesma instituição, ou compartilhada com
// todo mundo) — usado em toda tela que lista questões pra escolher/usar
// (banco de questões, montagem de prova). Centralizar isso numa extensão só
// evita que uma tela nova esqueça de aplicar a regra e vaze questão privada
// de outro professor.
//
// Importante: isso é só sobre LEITURA/USO. Quem pode editar/excluir/desativar
// uma questão continua sendo só quem criou (CriadoPorId), independente da
// visibilidade — isso é checado à parte, na hora da ação.
public static class QuestaoVisibilidade
{
    public static IQueryable<Questao> VisivelPara(this IQueryable<Questao> query, string? meuId, int? minhaInstituicaoId)
    {
        return query.Where(q =>
            q.CriadoPorId == meuId ||
            q.Visibilidade == VisibilidadeQuestao.Compartilhada ||
            (q.Visibilidade == VisibilidadeQuestao.Institucional
                && minhaInstituicaoId != null
                && q.CriadoPor!.InstituicaoId == minhaInstituicaoId));
    }
}
