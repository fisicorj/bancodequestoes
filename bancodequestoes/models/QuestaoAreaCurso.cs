namespace BancoQuestoes.Models;

// Vínculo muitos-pra-muitos: representa APLICABILIDADE ACADÊMICA, nunca permissão de
// acesso. Chave composta (QuestaoId+AreaCursoId) já garante não-duplicidade.
public class QuestaoAreaCurso
{
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    public int AreaCursoId { get; set; }
    public AreaCurso? AreaCurso { get; set; }
}
