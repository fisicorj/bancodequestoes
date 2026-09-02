namespace BancoQuestoes.Services;

// Retorno padrão de qualquer listagem paginada dos Services — evita cada
// página .razor ter que gerenciar "Itens" e "Total" como duas variáveis soltas
// (e evita repetir esse par em todo Service novo). Pensado pra ser genérico o
// bastante pra ser reusado por QuestaoService, ProvaService etc. também.
public sealed class PaginaResultado<T>
{
    public required List<T> Itens { get; init; }
    public required int Total { get; init; }
}
