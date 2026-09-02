namespace BancoQuestoes.Models;

public class Disciplina
{
    public int Id { get; set; }

    // "required" (C# 11+) obriga quem cria o objeto a preencher essa propriedade,
    // senão o compilador dá erro. É uma forma de garantir, em tempo de compilação,
    // que Nome nunca fica vazio por esquecimento.
    public required string Nome { get; set; }

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    // Propriedade de navegação: representa o relacionamento "um para muitos"
    // (uma Disciplina tem várias Assuntos). O EF Core usa isso para gerar
    // a FK no banco e para permitir Disciplina.Assuntos na sua consulta,
    // sem você escrever o JOIN manualmente.
    public List<Assunto> Assuntos { get; set; } = new();
}
