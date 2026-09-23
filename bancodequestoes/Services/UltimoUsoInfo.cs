using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// "Prova X — Tipo — dd/MM/aaaa": resumo da utilização mais recente de uma
// questão, derivado de ProvaQuestao/Prova (sem tabela própria).
public sealed class UltimoUsoInfo
{
    public required string Titulo { get; set; }
    public TipoProva? Tipo { get; set; }
    public DateOnly? Data { get; set; }

    public string Rotulo
    {
        get
        {
            var partes = new List<string> { Titulo };
            if (Tipo is { } tipo)
            {
                partes.Add(tipo.Rotulo());
            }
            if (Data is { } data)
            {
                partes.Add(data.ToString("dd/MM/yyyy"));
            }
            return string.Join(" — ", partes);
        }
    }
}
