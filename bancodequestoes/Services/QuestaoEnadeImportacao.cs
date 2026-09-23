using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Preparação pra uma futura importação estruturada de provas ENADE — só o
// formato de entrada (QuestaoImportacaoEnadeDto) e a deduplicação (QuestaoEnadeChaveNatural); sem parser ainda.

// Formato JSON por questão: Formação Geral usa DisciplinaCodigo (aponta pra
// Disciplina global sem descobrir o Id); Componente Específico usa DisciplinaId + AreaCursoIds.
public sealed class QuestaoImportacaoEnadeDto
{
    public OrigemQuestao Origem { get; set; } = OrigemQuestao.Enade;
    public int AnoOrigem { get; set; }
    public string NumeroOriginal { get; set; } = "";
    public SecaoEnade SecaoEnade { get; set; }
    public string? DisciplinaCodigo { get; set; }
    public int? DisciplinaId { get; set; }
    public List<int> AreaCursoIds { get; set; } = new();
    public string? CodigoProvaOrigem { get; set; }
    public string? Enunciado { get; set; }

    // AssuntoId final fica pra quem implementar o importador de verdade.
}

// Chave natural pra detectar duplicata — nunca vira constraint de banco, só
// helper de serviço. Componente Específico soma a Área (Formação Geral não tem).
public static class QuestaoEnadeChaveNatural
{
    // Chave textual só pra log/exibição — as consultas reais comparam os campos direto no banco.
    public static string Calcular(int ano, SecaoEnade secao, string numeroOriginal, int? areaCursoId)
    {
        var numero = (numeroOriginal ?? "").Trim().ToUpperInvariant();
        return secao == SecaoEnade.FormacaoGeral
            ? $"ENADE|{ano}|FormacaoGeral|{numero}"
            : $"ENADE|{ano}|ComponenteEspecifico|Area{areaCursoId}|{numero}";
    }

    // Devolve a Questao existente com a mesma chave, se houver (a tela decide o que fazer). areaCursoId
    // é obrigatório pra Componente Específico — sem ele, null.
    public static async Task<Questao?> EncontrarPossivelDuplicataAsync(
        ApplicationDbContext db, int ano, SecaoEnade secao, string numeroOriginal, int? areaCursoId)
    {
        var numero = (numeroOriginal ?? "").Trim();
        if (numero.Length == 0)
        {
            return null;
        }

        var query = db.Questoes
            .Where(q => q.Origem == OrigemQuestao.Enade && q.Ano == ano && q.SecaoEnade == secao && q.NumeroOriginal == numero);

        if (secao == SecaoEnade.ComponenteEspecifico)
        {
            if (areaCursoId is not int aid)
            {
                return null;
            }
            query = query.Where(q => q.AreasCurso.Any(a => a.Id == aid));
        }

        return await query.FirstOrDefaultAsync();
    }
}
