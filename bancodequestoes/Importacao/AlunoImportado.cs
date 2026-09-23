namespace BancoQuestoes.Importacao;

// Modelo "achatado" de UMA linha de aluno interpretada do CSV — ainda não é
// entidade do banco, só o que a pré-visualização/importação precisam.
public class AlunoImportado
{
    public required string Nome { get; set; }
    public string? Email { get; set; }
    public string? Matricula { get; set; }

    // Selecionado por padrão na pré-visualização; o usuário pode desmarcar
    // antes de confirmar a importação.
    public bool Selecionado { get; set; } = true;
}

// Resultado completo de uma importação: alunos reconhecidos + erros de linhas
// que não deu pra interpretar (mostrados à parte, sem travar o resto).
public class ResultadoImportacaoAlunos
{
    public List<AlunoImportado> Alunos { get; set; } = new();
    public List<string> Erros { get; set; } = new();
}
