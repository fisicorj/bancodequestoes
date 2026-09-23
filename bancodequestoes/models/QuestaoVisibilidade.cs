using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Filtro central de "quem pode ENXERGAR essa questão", pra nenhuma tela esquecer a
// regra. Só sobre LEITURA — editar/excluir continua restrito a quem criou.
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
