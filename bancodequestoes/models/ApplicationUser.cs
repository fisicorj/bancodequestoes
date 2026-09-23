using Microsoft.AspNetCore.Identity;

namespace BancoQuestoes.Models;

// Herda de IdentityUser (padrão recomendado) pra adicionar campos próprios sem
// mexer no pacote do Identity.
public class ApplicationUser : IdentityUser
{
    public string? NomeCompleto { get; set; }

    // Opcional: sem instituição, a visibilidade "Institucional" das questões
    // não tem como saber quem é "da mesma instituição" de quem.
    public int? InstituicaoId { get; set; }
    public Instituicao? Instituicao { get; set; }
}
