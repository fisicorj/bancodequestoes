using Microsoft.AspNetCore.Components;

namespace BancoQuestoes.Components.Account;

// Componentes Blazor renderizados de forma estática (sem @rendermode) rodam dentro
// de uma requisição HTTP normal, então um redirect comum funciona. Esse helper existe
// para deixar isso explícito e centralizado nas páginas de login/registro, que
// dependem de rodar em modo estático (não InteractiveServer) para poder escrever
// o cookie de autenticação na resposta.
//
// O projeto tem <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
// no .csproj (padrão do template .NET 10+), então NavigateTo NÃO lança mais exceção
// durante renderização estática — ele só agenda o redirect e o código continua
// executando normalmente depois da chamada.
internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public void RedirectTo(string? uri)
    {
        uri ??= "";

        // Evita open redirect: só aceita URIs relativas.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }
}
