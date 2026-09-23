namespace BancoQuestoes.Models;

// Catálogo NACIONAL de área de curso, desacoplado de Instituicao: uma Matriz ENADE/DCN
// vale pra qualquer instituição com a área; Curso.AreaCursoId faz esse elo.
public class AreaCurso
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
