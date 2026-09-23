using System.ComponentModel.DataAnnotations;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// DTO da tela de Prova; validação e persistência ficam no Service.
public sealed class ProvaInput
{
    [Required(ErrorMessage = "Informe o título.")]
    public string Titulo { get; set; } = "";

    // Modo Disciplina (padrão) — único campo que a tela simples preenche. Nos
    // modos Multidisciplinar/Curso, o Service usa DisciplinaIds/TipoEscopo abaixo.
    public int DisciplinaId { get; set; }

    // Escopo da prova (ver TipoEscopoProva/EscopoProvaSelector.razor);
    // default Disciplina preserva o comportamento de sempre.
    public TipoEscopoProva TipoEscopo { get; set; } = TipoEscopoProva.Disciplina;

    // Só usado no modo Multidisciplinar — as 2+ disciplinas selecionadas.
    public HashSet<int> DisciplinaIds { get; set; } = new();

    // Distribuição planejada opcional por disciplina (chave = DisciplinaId,
    // valor = %); vazio = sem distribuição.
    public Dictionary<int, int> DistribuicaoDisciplinas { get; set; } = new();

    // Matriz de Referência usada na geração (modo Curso, tipicamente ENADE),
    // só pra auditoria. Zero/null = Curso "livre", sem matriz.
    public int? MatrizReferenciaId { get; set; }

    public int CursoId { get; set; }
    public int TurmaId { get; set; }
    public TipoProva? Tipo { get; set; }
    public int? Ano { get; set; }
    public int? Semestre { get; set; }
    public int? Bimestre { get; set; }
    public DateOnly? DataAplicacao { get; set; }
    public int? TempoEstimadoMinutos { get; set; }
    public string? Observacoes { get; set; }

    // Espelha Prova.MostrarValorNoEnunciado — default true preserva o comportamento de sempre.
    public bool MostrarValorNoEnunciado { get; set; } = true;
}

// Questão escolhida pra compor a prova — estado da tela antes de virar ProvaQuestao no Salvar.
public sealed class QuestaoSelecionada
{
    public int QuestaoId { get; set; }
    public int Ordem { get; set; }
    public decimal? Valor { get; set; }
}

// Como distribuir o "Valor da prova" entre as questões — top-level pro
// componente PontuacaoProva.razor também conseguir referenciar.
public enum ModoPontuacao
{
    MesmoValor,
    ProporcionalDificuldade,
    Manual,
}
