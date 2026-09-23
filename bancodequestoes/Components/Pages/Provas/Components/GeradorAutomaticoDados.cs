using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.AspNetCore.Components;

namespace BancoQuestoes.Components.Pages.Provas.Components;

// Agrupa os ~37 [Parameter] que GeradorAutomatico.razor recebia soltos —
// Etapa 2b da redução de tamanho de ProvaForm/GeradorAutomatico (complementar
// à extração de cálculo pra CalculadoraGeradorAutomatico, na Etapa 2a).
// ProvaForm continua dono de todo o estado e de toda a lógica — só monta este
// objeto (ver MontarDadosGerador em ProvaForm.razor.cs). GeradorAutomatico.razor
// também não muda por dentro: os nomes de sempre (Gerador, Diagnostico,
// FaceisQtd etc.) viram propriedades "pass-through" pra este objeto único no
// topo do @code, então o corpo do markup e do resto do @code fica idêntico.
public sealed class GeradorAutomaticoDados
{
    public required GeradorInput Gerador { get; init; }
    public DiagnosticoBanco Diagnostico { get; init; } = new();
    public int FaceisQtd { get; init; }
    public int MediasQtd { get; init; }
    public int DificeisQtd { get; init; }
    public int TotalPercDificuldade { get; init; }

    public List<Assunto> AssuntosDisponiveis { get; init; } = new();
    public required Func<int, int> DisponiveisPorAssunto { get; init; }
    public required Func<int, int> QuantidadePorAssunto { get; init; }
    public int TotalPercAssuntos { get; init; }
    public List<string> AvisosDisponibilidade { get; init; } = new();

    public List<TipoQuestao> TiposPermitidosAtuais { get; init; } = new();
    public required Func<TipoQuestao, int> DisponiveisPorTipo { get; init; }
    public int SomaQuantidadePorTipo { get; init; }
    public List<string> AvisosPorTipo { get; init; } = new();

    public int TotalPercBloom { get; init; }

    public int QuantidadeQualidadeAtende { get; init; }

    public List<ItemMatrizReferencia> ItensCurricular { get; init; } = new();
    public required Func<int, int> DisponiveisPorItem { get; init; }
    public List<LinhaCoberturaItem> CoberturaCurricular { get; init; } = new();
    public int DisponiveisDistintasSelecionadas { get; init; }

    public Dictionary<int, int> QtdPlanejadaPorItemBlueprint { get; init; } = new();
    public bool BlueprintCurricularViavel { get; init; }
    public List<string> AvisosBlueprintCurricular { get; init; } = new();
    public List<LinhaBlueprintCurricular> AderenciaBlueprintCurricular { get; init; } = new();
    public int AderenciaBlueprintCurricularPercentual { get; init; }
    public EventCallback<(int ItemId, bool Marcado)> OnAlternarItemBlueprint { get; init; }

    public int? TempoMinutos { get; init; }
    public string ResumoConteudo { get; init; } = "";
    public string TiposTexto { get; init; } = "";
    public string? AvisoGerador { get; init; }
    public int SelecionadasCount { get; init; }
    public List<LinhaAderencia> Aderencia { get; init; } = new();

    public EventCallback OnAlterado { get; init; }
    public EventCallback<(int AssuntoId, bool Marcado)> OnAlternarAssunto { get; init; }
    public EventCallback OnSincronizarQuantidadePorTipo { get; init; }
    public EventCallback OnAlternarDefinirQuantidadePorTipo { get; init; }
    public EventCallback<(NivelBloom Nivel, bool Marcado)> OnAlternarBloom { get; init; }
    public EventCallback OnAtualizarDiagnosticoRecentes { get; init; }
    public EventCallback OnGerar { get; init; }
    public EventCallback OnAdicionarPeloGerador { get; init; }
}
