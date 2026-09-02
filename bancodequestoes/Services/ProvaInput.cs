using System.ComponentModel.DataAnnotations;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Movido de ProvaForm.razor (classe privada) pro Service dono da validação/
// persistência — mesmo padrão do QuestaoInput.
public sealed class ProvaInput
{
    [Required(ErrorMessage = "Informe o título.")]
    public string Titulo { get; set; } = "";

    public int DisciplinaId { get; set; }
    public int CursoId { get; set; }
    public int TurmaId { get; set; }
    public TipoProva? Tipo { get; set; }
    public int? Ano { get; set; }
    public int? Semestre { get; set; }
    public int? Bimestre { get; set; }
    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }
    public string? Observacoes { get; set; }
}

// Uma questão escolhida pra compor a prova (seleção manual ou pelo gerador),
// com a ordem de exibição e o valor atribuído — não é uma entidade EF, só o
// estado da tela antes de virar ProvaQuestao no Salvar.
public sealed class QuestaoSelecionada
{
    public int QuestaoId { get; set; }
    public int Ordem { get; set; }
    public decimal? Valor { get; set; }
}

// Como distribuir o "Valor da prova" entre as questões selecionadas (item 16
// do redesenho de ProvaForm.razor) — top-level (não mais aninhado como
// "private enum" dentro de ProvaForm) pra o componente PontuacaoProva.razor
// também conseguir referenciar, já que um enum privado de uma classe não é
// visível de outro arquivo.
public enum ModoPontuacao
{
    MesmoValor,
    ProporcionalDificuldade,
    Manual,
}
