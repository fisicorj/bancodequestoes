namespace BancoQuestoes.Models;

// Curso pertence a uma Instituicao. Vincular a Prova a um Curso (em vez de à
// Instituicao direto) resolve as duas coisas de uma vez: já traz o cabeçalho
// certo (via Curso.Instituicao) e preenche o campo "Curso" no documento.
public class Curso
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    public int InstituicaoId { get; set; }
    public Instituicao? Instituicao { get; set; }

    // Opcional: qual Área de Curso nacional (ver AreaCurso) este curso desta
    // instituição representa — ex.: "Engenharia de Computação da UFMG"
    // aponta pra AreaCurso "Engenharia de Computação". É o que permite uma
    // Matriz ENADE/DCN (ligada à AreaCurso, não a este Curso) valer pra
    // qualquer instituição que tenha a mesma área, sem reimportar. Fica
    // vazio até o professor escolher deliberadamente — nunca é inferido/
    // criado sozinho a partir do Nome (mesmo princípio de "nunca criar
    // automaticamente" já usado na importação de matriz completa).
    public int? AreaCursoId { get; set; }
    public AreaCurso? AreaCurso { get; set; }
}
