using Microsoft.AspNetCore.Identity;

namespace BancoQuestoes.Models;

// IdentityUser já vem com Id, Email, PasswordHash, etc.
// Herdar dele (em vez de usar o IdentityUser puro) é o padrão recomendado pela Microsoft,
// porque permite adicionar campos próprios sem mexer no pacote do Identity.
public class ApplicationUser : IdentityUser
{
    // "string?" com a interrogação é um "nullable reference type": diz ao compilador
    // explicitamente que esse campo pode ser nulo. Sem o "?", o compilador espera
    // (e avisa com warning) que o campo sempre tenha valor.
    public string? NomeCompleto { get; set; }

    // Opcional de propósito: nem todo professor precisa vincular a uma
    // instituição, mas sem isso a visibilidade "Institucional" das questões
    // não tem como saber quem é "da mesma instituição" de quem.
    public int? InstituicaoId { get; set; }
    public Instituicao? Instituicao { get; set; }
}
