namespace BancoQuestoes.Models;

// Catálogo NACIONAL de área de curso (ex.: "Engenharia de Computação") —
// desacoplado de Instituicao de propósito. Existe pra resolver um problema
// real: uma Matriz ENADE/DCN (ver MatrizReferencia) é um documento do
// MEC/INEP, igual pra qualquer instituição que tenha aquele curso — não faz
// sentido reimportar "ENADE Engenharia de Computação 2023" uma vez por
// instituição, nem uma questão classificada como C08 numa instituição
// "não contar" pra outra instituição com o mesmo curso.
//
// Curso (por instituição, ver Curso.AreaCursoId) pode opcionalmente apontar
// pra uma AreaCurso — é o elo entre "o curso desta instituição" e "a área
// nacional que ele representa". MatrizReferencia de Tipo ENADE/DCN aponta
// direto pra AreaCurso (não pra Curso) — ver MatrizReferencia.AreaCursoId.
// PPC/Institucional/Outro continuam presos a um Curso específico, porque
// esses SÃO documentos de uma instituição, não nacionais.
public class AreaCurso
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
