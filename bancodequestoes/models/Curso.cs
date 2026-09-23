namespace BancoQuestoes.Models;

// Vincular a Prova a um Curso (em vez de à Instituicao direto) já traz o cabeçalho
// certo (via Curso.Instituicao) e preenche o campo "Curso" no documento.
public class Curso
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public int InstituicaoId { get; set; }
    public Instituicao? Instituicao { get; set; }

    // Opcional: qual AreaCurso nacional este curso representa — permite uma Matriz
    // ENADE/DCN valer pra qualquer instituição com a mesma área. Nunca inferido do Nome.
    public int? AreaCursoId { get; set; }
    public AreaCurso? AreaCurso { get; set; }
}
