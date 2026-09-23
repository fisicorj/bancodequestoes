namespace BancoQuestoes.Services;

// Retorno padrão de listagens paginadas dos Services (Itens + Total).
public sealed class PaginaResultado<T>
{
    public required List<T> Itens { get; init; }
    public required int Total { get; init; }
}
