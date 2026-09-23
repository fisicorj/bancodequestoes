using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BancoQuestoes.Components.Pages.Provas.Components;
using BancoQuestoes.Models;
using BancoQuestoes.Services;

namespace BancoQuestoes.Components.Pages.Provas;

// Code-behind de ProvaForm.razor — só move o @code pra cá (classe parcial nativa
// do Blazor), sem nenhuma mudança de comportamento, assinatura ou parâmetro de
// componente filho. O .razor fica só com o markup; a lógica mora aqui.
public partial class ProvaForm
{
    [Parameter]
    public int? Id { get; set; }

    private ProvaInput modelo = new();
    private List<Disciplina> disciplinas = new();
    private List<Curso> cursos = new();
    private List<Turma> todasTurmas = new();
    private List<Assunto> assuntosDisponiveis = new();

    // Itens (flat, de todas as matrizes vinculáveis) do Curso escolhido na
    // prova, recarregados toda vez que ele muda.
    private List<ItemMatrizReferencia> itensCurricularProva = new();

    // Matrizes vinculáveis ao Curso escolhido, recarregadas a cada troca de Curso;
    // tipoEscopoAnterior é o "desfazer" de AoMudarEscopoAsync.
    private List<MatrizReferencia> matrizesDoCurso = new();
    private TipoEscopoProva tipoEscopoAnterior = TipoEscopoProva.Disciplina;

    // Monta o contexto (somente leitura) que CalculadoraGeradorAutomatico usa —
    // Etapa 2 da redução de ProvaForm.razor.cs: o cluster de cálculo do
    // Blueprint/Diagnóstico do Gerador Automático (antes ~400 linhas aqui)
    // virou uma classe colaboradora dedicada (Services/CalculadoraGeradorAutomatico.cs).
    // Os nomes/assinaturas abaixo continuam os mesmos de sempre — markup e
    // GeradorAutomatico.razor não mudam nada.
    private ContextoCalculoGerador Ctx() => new()
    {
        GeradorProvaService = GeradorProvaService,
        Gerador = gerador,
        QuestoesDisponiveis = questoesDisponiveis,
        AssuntosDisponiveis = assuntosDisponiveis,
        ItensCurricular = itensCurricularProva,
        Selecionadas = selecionadas,
        QuestoesPorId = questoesPorId,
        IdsRecentesDiagnostico = idsRecentesDiagnostico,
    };

    // Etapa 2b: monta o único [Parameter] que GeradorAutomatico.razor recebe
    // agora — antes eram ~37 atributos soltos na tag <GeradorAutomatico ... />
    // em ProvaForm.razor, cada um com a MESMA expressão que está aqui embaixo.
    // Como no Razor cada atributo já era recalculado a cada render de
    // ProvaForm, empacotar tudo num objeto não muda quantas vezes essas
    // contas rodam — só reduz a superfície de parâmetros do componente filho.
    private GeradorAutomaticoDados MontarDadosGerador() => new()
    {
        Gerador = gerador,
        Diagnostico = CalcularDiagnostico(),
        FaceisQtd = QuantidadePorDificuldade(Dificuldade.Facil),
        MediasQtd = QuantidadePorDificuldade(Dificuldade.Media),
        DificeisQtd = QuantidadePorDificuldade(Dificuldade.Dificil),
        TotalPercDificuldade = TotalPercDificuldade,

        AssuntosDisponiveis = assuntosDisponiveis,
        DisponiveisPorAssunto = DisponiveisPorAssunto,
        QuantidadePorAssunto = QuantidadePorAssunto,
        TotalPercAssuntos = TotalPercAssuntos,
        AvisosDisponibilidade = AvisosDisponibilidade(),

        TiposPermitidosAtuais = gerador.TiposPermitidos(),
        DisponiveisPorTipo = DisponiveisPorTipo,
        SomaQuantidadePorTipo = SomaQuantidadePorTipo,
        AvisosPorTipo = AvisosPorTipo(),

        TotalPercBloom = TotalPercBloom,

        QuantidadeQualidadeAtende = QuantidadeQualidadeAtende(),

        ItensCurricular = itensCurricularProva,
        DisponiveisPorItem = DisponiveisPorItem,
        CoberturaCurricular = CalcularCoberturaCurricular(),
        DisponiveisDistintasSelecionadas = DisponiveisDistintasParaConjunto(),

        QtdPlanejadaPorItemBlueprint = QtdPlanejadaPorItemBlueprint(),
        BlueprintCurricularViavel = BlueprintCurricularViavel(),
        AvisosBlueprintCurricular = AvisosBlueprintCurricular(),
        AderenciaBlueprintCurricular = CalcularAderenciaBlueprintCurricular(),
        AderenciaBlueprintCurricularPercentual = AderenciaBlueprintCurricularPercentual(),
        OnAlternarItemBlueprint = EventCallback.Factory.Create<(int ItemId, bool Marcado)>(this, args => AlternarItemBlueprint(args.ItemId, args.Marcado)),

        TempoMinutos = modelo.TempoEstimadoMinutos,
        ResumoConteudo = ResumoConteudoBlueprint(),
        TiposTexto = TiposPermitidosTexto(),
        AvisoGerador = avisoGerador,
        SelecionadasCount = selecionadas.Count,
        Aderencia = CalcularAderenciaBlueprint(),

        OnAlterado = EventCallback.Factory.Create(this, NotificarMudanca),
        OnAlternarAssunto = EventCallback.Factory.Create<(int AssuntoId, bool Marcado)>(this, args => AlternarAssuntoGerador(args.AssuntoId, args.Marcado)),
        OnSincronizarQuantidadePorTipo = EventCallback.Factory.Create(this, SincronizarQuantidadePorTipo),
        OnAlternarDefinirQuantidadePorTipo = EventCallback.Factory.Create(this, AoAlternarDefinirQuantidadePorTipo),
        OnAlternarBloom = EventCallback.Factory.Create<(NivelBloom Nivel, bool Marcado)>(this, args => AlternarBloomGerador(args.Nivel, args.Marcado)),
        OnAtualizarDiagnosticoRecentes = EventCallback.Factory.Create(this, AtualizarDiagnosticoRecentesAsync),
        OnGerar = EventCallback.Factory.Create(this, GerarProvaAsync),
        OnAdicionarPeloGerador = EventCallback.Factory.Create(this, AdicionarPeloGeradorAsync),
    };

    private int TotalPercAssuntos => CalculadoraGeradorAutomatico.TotalPercAssuntos(Ctx());
    private int TotalPercBloom => CalculadoraGeradorAutomatico.TotalPercBloom(Ctx());
    private int TotalPercDificuldade => CalculadoraGeradorAutomatico.TotalPercDificuldade(Ctx());

    private int QuantidadePorDificuldade(Dificuldade nivel) =>
        CalculadoraGeradorAutomatico.QuantidadePorDificuldade(Ctx(), nivel);

    private int DisponiveisPorAssunto(int assuntoId) =>
        CalculadoraGeradorAutomatico.DisponiveisPorAssunto(Ctx(), assuntoId);

    private HashSet<int> ItensCurricularSelecionados() =>
        CalculadoraGeradorAutomatico.ItensCurricularSelecionados(Ctx());

    private int DisponiveisDistintasParaConjunto() =>
        CalculadoraGeradorAutomatico.DisponiveisDistintasParaConjunto(Ctx());

    private int DisponiveisPorItem(int itemId) =>
        CalculadoraGeradorAutomatico.DisponiveisPorItem(Ctx(), itemId);

    private List<LinhaCoberturaItem> CalcularCoberturaCurricular() =>
        CalculadoraGeradorAutomatico.CalcularCoberturaCurricular(Ctx());

    private (bool Possivel, Dictionary<int, int> FaltandoPorItem) SimularViabilidadeBlueprint() =>
        CalculadoraGeradorAutomatico.SimularViabilidadeBlueprint(Ctx());

    private List<LinhaBlueprintCurricular> CalcularAderenciaBlueprintCurricular() =>
        CalculadoraGeradorAutomatico.CalcularAderenciaBlueprintCurricular(Ctx());

    private int AderenciaBlueprintCurricularPercentual() =>
        CalculadoraGeradorAutomatico.AderenciaBlueprintCurricularPercentual(Ctx());

    private Dictionary<int, int> QtdPlanejadaPorItemBlueprint() =>
        CalculadoraGeradorAutomatico.QtdPlanejadaPorItemBlueprint(Ctx());

    private bool BlueprintCurricularViavel() =>
        CalculadoraGeradorAutomatico.BlueprintCurricularViavel(Ctx());

    private List<string> AvisosBlueprintCurricular() =>
        CalculadoraGeradorAutomatico.AvisosBlueprintCurricular(Ctx());

    // Entra com 0% (o professor ajusta depois); precisa ser Dictionary (não
    // HashSet como no Modo Filtro) porque cada item carrega um percentual.
    // Mutador direto de "gerador" — fica em ProvaForm (não é cálculo puro).
    private void AlternarItemBlueprint(int itemId, bool marcado)
    {
        if (marcado)
        {
            if (!gerador.PercPorItem.ContainsKey(itemId))
            {
                gerador.PercPorItem[itemId] = 0;
            }
        }
        else
        {
            gerador.PercPorItem.Remove(itemId);
        }
    }

    private int QuantidadeQualidadeAtende() =>
        CalculadoraGeradorAutomatico.QuantidadeQualidadeAtende(Ctx());

    private List<Questao> PoolDiagnostico() =>
        CalculadoraGeradorAutomatico.PoolDiagnostico(Ctx());

    private DiagnosticoBanco CalcularDiagnostico() =>
        CalculadoraGeradorAutomatico.CalcularDiagnostico(Ctx());

    private List<LinhaAderencia> CalcularAderenciaBlueprint() =>
        CalculadoraGeradorAutomatico.CalcularAderenciaBlueprint(Ctx());

    private int DisponiveisPorTipo(TipoQuestao tipo) =>
        CalculadoraGeradorAutomatico.DisponiveisPorTipo(Ctx(), tipo);

    private int SomaQuantidadePorTipo => CalculadoraGeradorAutomatico.SomaQuantidadePorTipo(Ctx());

    // Chamado no @bind:after de cada checkbox de tipo, pra tabela nunca mostrar
    // (ou perder) um tipo fora de sincronia com o que está permitido.
    // Mutador direto de "gerador" — fica em ProvaForm (não é cálculo puro).
    private void SincronizarQuantidadePorTipo()
    {
        if (!gerador.DefinirQuantidadePorTipo)
        {
            return;
        }

        var tipos = gerador.TiposPermitidos();
        foreach (var tipo in tipos.Where(t => !gerador.QuantidadePorTipo.ContainsKey(t)))
        {
            gerador.QuantidadePorTipo[tipo] = 0;
        }
        foreach (var tipo in gerador.QuantidadePorTipo.Keys.Where(t => !tipos.Contains(t)).ToList())
        {
            gerador.QuantidadePorTipo.Remove(tipo);
        }
    }

    // Ao ligar pela primeira vez, começa com a Quantidade dividida igualmente
    // entre os tipos permitidos, pro professor ajustar em vez de partir do zero.
    // Mutador direto de "gerador" — fica em ProvaForm (não é cálculo puro).
    private void AoAlternarDefinirQuantidadePorTipo()
    {
        if (!gerador.DefinirQuantidadePorTipo || gerador.QuantidadePorTipo.Count > 0)
        {
            return;
        }

        gerador.QuantidadePorTipo = CalculadoraGeradorAutomatico.DividirQuantidadeIgualmente(gerador.TiposPermitidos(), gerador.Quantidade);
    }

    private List<string> AvisosPorTipo() =>
        CalculadoraGeradorAutomatico.AvisosPorTipo(Ctx());

    private int QuantidadePorAssunto(int assuntoId) =>
        CalculadoraGeradorAutomatico.QuantidadePorAssunto(Ctx(), assuntoId);

    private string ResumoConteudoBlueprint() =>
        CalculadoraGeradorAutomatico.ResumoConteudoBlueprint(Ctx());

    private string TiposPermitidosTexto() =>
        CalculadoraGeradorAutomatico.TiposPermitidosTexto(Ctx());

    private List<string> AvisosDisponibilidade() =>
        CalculadoraGeradorAutomatico.AvisosDisponibilidade(Ctx());

    private List<Questao> questoesDisponiveis = new();
    private List<Questao> questoesFiltradas = new();
    private Dictionary<int, Questao> questoesPorId = new();
    private List<QuestaoSelecionada> selecionadas = new();
    private int filtroAssuntoId;
    private bool carregando;
    private string? erro;
    private bool acessoNegado;
    private string? meuId;
    private int? minhaInstituicaoId;

    // Carregado uma vez junto com questoesDisponiveis e reaproveitado pelos cards
    // e pelo sorteio, em vez de consultar o banco de novo a cada "Gerar prova".
    private Dictionary<int, int> usosPorQuestaoDisponiveis = new();

    // Mostrado mesmo com "Evitar questões recentes" desmarcado, é só informativo
    // até o professor ligar o filtro de verdade.
    private HashSet<int> idsRecentesDiagnostico = new();

    // Sempre uma das já carregadas em questoesPorId, então não precisa de uma
    // consulta nova só pra abrir o modal.
    private Questao? questaoPreview;

    private GeradorInput gerador = new();
    private string? avisoGerador;

    // Escolhido numa telinha própria assim que a Disciplina é definida, pra não
    // empilhar o card do gerador e a busca manual na mesma tela.
    private enum ModoMontagem { NaoEscolhido, Automatico, Manual }
    private ModoMontagem modo = ModoMontagem.NaoEscolhido;

    // MesmoValor é o comportamento antigo (botão único "Distribuir valor");
    // Manual desliga a distribuição automática e o professor edita cada Valor.
    private ModoPontuacao modoPontuacao = ModoPontuacao.MesmoValor;
    private string? avisoPontuacao;

    // Enquanto modelo.Titulo continuar igual a isso (ou vazio), a sugestão
    // continua atualizando sozinha; edição manual do professor interrompe.
    private string? tituloAutoSugerido;

    // Último DisciplinaId efetivamente carregado — ver AoMudarDisciplinaAsync.
    private int disciplinaIdAnterior;

    // Restringe as opções de Turma ao Curso já escolhido; sem Curso ainda,
    // mostra todas (o professor pode escolher a Turma primeiro).
    private List<Turma> TurmasDoCurso => modelo.CursoId == 0
        ? todasTurmas
        : todasTurmas.Where(t => t.CursoId == modelo.CursoId).ToList();

    // cursos já vem com .Instituicao incluída, então decide sem consulta extra;
    // ao contrário de Turma, aqui não é obrigatório escolher.
    private bool PrecisaBimestre =>
        cursos.FirstOrDefault(c => c.Id == modelo.CursoId)?.Instituicao?.SistemaPeriodos == SistemaPeriodos.SemestralComBimestres;

    protected override async Task OnInitializedAsync()
    {
        carregando = true;

        var authState = await AuthProvider.GetAuthenticationStateAsync();
        meuId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
        minhaInstituicaoId = await ProvaService.ObterInstituicaoDoUsuarioAsync(meuId);

        disciplinas = await DisciplinaService.ListarTodasAsync();
        cursos = await CursoService.ListarTodosAsync();
        todasTurmas = await CursoService.ListarTodasTurmasAsync();

        if (Id is not null)
        {
            var prova = await ProvaService.CarregarComQuestoesAsync(Id.Value);

            if (prova is null)
            {
                erro = "Prova não encontrada.";
                acessoNegado = true;
            }
            else if (prova.CriadoPorId != meuId)
            {
                // Provas são privadas: só quem criou pode ver/editar/apagar a sua.
                erro = "Você não tem permissão para editar esta prova.";
                acessoNegado = true;
            }
            else
            {
                modelo.Titulo = prova.Titulo;

                // Reconstrói o escopo a partir do que foi persistido; uma prova antiga
                // (pré-migração) cai direto no ramo Disciplina sem perda de dado.
                modelo.TipoEscopo = prova.TipoEscopo;
                modelo.DisciplinaId = prova.DisciplinaId ?? 0;
                modelo.DisciplinaIds = prova.ProvaDisciplinas.Select(pd => pd.DisciplinaId).ToHashSet();
                modelo.DistribuicaoDisciplinas = prova.ProvaDisciplinas
                    .Where(pd => pd.PercentualPlanejado is not null)
                    .ToDictionary(pd => pd.DisciplinaId, pd => pd.PercentualPlanejado!.Value);
                modelo.MatrizReferenciaId = prova.MatrizReferenciaId;
                tipoEscopoAnterior = modelo.TipoEscopo;

                modelo.CursoId = prova.CursoId ?? 0;
                modelo.TurmaId = prova.TurmaId ?? 0;
                await CarregarMatrizesDoCursoAsync();
                await CarregarItensDoCursoProvaAsync();
                modelo.Tipo = prova.Tipo;
                modelo.Ano = prova.Ano;
                modelo.Semestre = prova.Semestre;
                modelo.Bimestre = prova.Bimestre;
                modelo.DataAplicacao = prova.DataAplicacao;
                modelo.TempoEstimadoMinutos = prova.TempoEstimadoMinutos;
                modelo.Observacoes = prova.Observacoes;
                modelo.MostrarValorNoEnunciado = prova.MostrarValorNoEnunciado;

                await CarregarQuestoesDisponiveisAsync();

                foreach (var pq in prova.ProvaQuestoes.OrderBy(pq => pq.Ordem))
                {
                    if (pq.Questao is not null)
                    {
                        questoesPorId[pq.Questao.Id] = pq.Questao;
                    }
                    selecionadas.Add(new QuestaoSelecionada
                    {
                        QuestaoId = pq.QuestaoId,
                        Ordem = pq.Ordem,
                        Valor = pq.Valor,
                    });
                }

                // Pula direto pro modo manual (já foi decidido antes); o professor
                // ainda pode trocar pra "Gerar automaticamente" se quiser.
                modo = ModoMontagem.Manual;
            }
        }

        carregando = false;
    }

    // Usado sempre que trocar de escopo de verdade: outro escopo = outras
    // questões, a seleção antiga não faz mais sentido.
    private async Task CarregarQuestoesDisponiveisAsync()
    {
        selecionadas.Clear();
        await RecarregarPoolAsync();
    }

    // Recarrega só o POOL (candidatas/diagnóstico/filtro), sem tocar em
    // "selecionadas" — a seleção antiga continua valendo até o Salvar validar.
    private async Task RecarregarPoolAsync()
    {
        SincronizarEscopoGerador();

        var escopo = gerador.Escopo;
        var (assuntos, questoes) = escopo.Validar() is null
            ? await QuestaoQueryService.ObterCandidatasAsync(escopo.ParaEscopoQuestoes(), meuId, minhaInstituicaoId)
            : (new List<Assunto>(), new List<Questao>());

        assuntosDisponiveis = assuntos;
        questoesDisponiveis = questoes;

        foreach (var q in questoesDisponiveis)
        {
            questoesPorId[q.Id] = q;
        }

        // Todos os assuntos começam marcados com % dividido igualmente; o
        // professor ajusta a partir daí em vez de preencher tudo do zero.
        gerador.PercPorAssunto = DividirIgualmente(assuntosDisponiveis.Select(a => a.Id).ToList());

        usosPorQuestaoDisponiveis = await ProvaService.ContarUsosAsync(questoesDisponiveis.Select(q => q.Id).ToList());
        await AtualizarDiagnosticoRecentesAsync();

        filtroAssuntoId = 0;
        AplicarFiltro();

        AtualizarSugestaoTitulo();

        // Referência pra AoMudarDisciplinaAsync/AoMudarTurmaAsync voltarem o
        // <select> a este valor se o professor desistir da troca.
        disciplinaIdAnterior = modelo.DisciplinaId;
    }

    // Mantém gerador.Escopo em dia com modelo pra nunca gerar com um escopo
    // desatualizado. EscopoProvaConfig.DoProvaInput é a mesma tradução do ProvaService.
    private void SincronizarEscopoGerador()
    {
        gerador.Escopo = EscopoProvaConfig.DoProvaInput(modelo);
        gerador.DistribuicaoDisciplinas = new Dictionary<int, int>(modelo.DistribuicaoDisciplinas);
    }

    // Trocar de Disciplina descarta o pool e a seleção atual — confirma antes
    // e, se o professor desistir, desfaz o valor que o bind já tinha empurrado.
    private async Task AoMudarDisciplinaAsync()
    {
        if (!await ConfirmarLimpezaSelecaoAsync())
        {
            modelo.DisciplinaId = disciplinaIdAnterior;
            return;
        }

        await CarregarQuestoesDisponiveisAsync();
    }

    // Reaproveitado por quem mais limpa "selecionadas" ao trocar Disciplina
    // (hoje também AoMudarTurmaAsync); sem risco, pula o pop-up.
    private async Task<bool> ConfirmarLimpezaSelecaoAsync()
    {
        if (selecionadas.Count == 0)
        {
            return true;
        }

        return await JS.InvokeAsync<bool>("confirm",
            $"Alterar a disciplina removerá as {selecionadas.Count} questões atualmente selecionadas. Deseja continuar?");
    }

    // Mesmo cálculo do sorteio de verdade, disparado antecipadamente pra
    // alimentar o card de diagnóstico antes do professor clicar em gerar.
    private async Task AtualizarDiagnosticoRecentesAsync()
    {
        idsRecentesDiagnostico = await ProvaService.ObterIdsRecentesAsync(meuId, gerador.CriterioRecentes, gerador.ValorCriterioRecentes);
    }

    // Atualiza o diagnóstico de "usadas recentemente" na hora, senão o card
    // mostraria "0" (valor inicial) até a primeira mudança de critério.
    private async Task EscolherModoAutomaticoAsync()
    {
        modo = ModoMontagem.Automatico;
        await AtualizarDiagnosticoRecentesAsync();
    }

    // Junta só as partes já preenchidas (ex.: "P1 — EngSoft — 2º sem/2026").
    // Só mexe em modelo.Titulo enquanto ele ainda for a última sugestão nossa.
    private void AtualizarSugestaoTitulo()
    {
        if (!string.IsNullOrWhiteSpace(modelo.Titulo) && modelo.Titulo != tituloAutoSugerido)
        {
            return;
        }

        var partes = new List<string>();

        if (modelo.Tipo is { } tipo)
        {
            partes.Add(tipo.Rotulo());
        }

        var disciplina = disciplinas.FirstOrDefault(d => d.Id == modelo.DisciplinaId)?.Nome;
        if (!string.IsNullOrWhiteSpace(disciplina))
        {
            partes.Add(disciplina);
        }

        var curso = cursos.FirstOrDefault(c => c.Id == modelo.CursoId)?.Nome;
        if (!string.IsNullOrWhiteSpace(curso))
        {
            partes.Add(curso);
        }

        var periodo = FormatarPeriodo();
        if (periodo is not null)
        {
            partes.Add(periodo);
        }

        tituloAutoSugerido = partes.Count == 0 ? null : string.Join(" — ", partes);
        modelo.Titulo = tituloAutoSugerido ?? "";
    }

    // "2º bimestre/2026", "1º semestre/2026" ou só "2026" — Bimestre tem
    // prioridade sobre Semestre quando ambos estão preenchidos.
    private string? FormatarPeriodo()
    {
        var parte = modelo.Bimestre is { } bimestre
            ? $"{bimestre}º bimestre"
            : modelo.Semestre is { } semestre
                ? $"{semestre}º semestre"
                : null;

        if (parte is null)
        {
            return modelo.Ano?.ToString();
        }

        return modelo.Ano is { } ano ? $"{parte}/{ano}" : parte;
    }

    // Ponto de partida pro gerador; GeradorProvaService.DistribuirPorPercentual
    // é quem decide a alocação final de verdade na hora do sorteio.
    private static Dictionary<int, int> DividirIgualmente(List<int> chaves)
    {
        var resultado = new Dictionary<int, int>();
        if (chaves.Count == 0)
        {
            return resultado;
        }

        var basePct = 100 / chaves.Count;
        var resto = 100 - basePct * chaves.Count;
        for (var i = 0; i < chaves.Count; i++)
        {
            resultado[chaves[i]] = basePct + (i < resto ? 1 : 0);
        }
        return resultado;
    }

    private void AlternarAssuntoGerador(int assuntoId, bool marcado)
    {
        if (marcado)
        {
            gerador.PercPorAssunto[assuntoId] = 0;
        }
        else
        {
            gerador.PercPorAssunto.Remove(assuntoId);
        }
    }

    private void AlternarBloomGerador(NivelBloom nivel, bool marcado)
    {
        if (marcado)
        {
            gerador.PercPorBloom[nivel] = 0;
        }
        else
        {
            gerador.PercPorBloom.Remove(nivel);
        }
    }

    // Escolher uma Turma implica Curso e Disciplina — preenche os dois pra
    // manter os três consistentes, e recarrega o pool se a Disciplina mudou.
    private async Task AoMudarTurmaAsync()
    {
        if (modelo.TurmaId == 0)
        {
            return;
        }

        var turma = todasTurmas.FirstOrDefault(t => t.Id == modelo.TurmaId);
        if (turma is null)
        {
            return;
        }

        // Mesmo aviso do <select> de Disciplina; desistindo, desfaz só a Turma.
        if (modelo.DisciplinaId != turma.DisciplinaId && !await ConfirmarLimpezaSelecaoAsync())
        {
            modelo.TurmaId = 0;
            return;
        }

        modelo.CursoId = turma.CursoId;
        modelo.Ano = turma.Ano;
        modelo.Semestre = turma.Semestre;
        modelo.Bimestre = turma.Bimestre;

        if (modelo.DisciplinaId != turma.DisciplinaId)
        {
            modelo.DisciplinaId = turma.DisciplinaId;
            await CarregarQuestoesDisponiveisAsync();
        }

        // Chama de novo pois Ano/Semestre/Bimestre podem ter mudado sem a
        // Disciplina mudar (idempotente, sem problema chamar 2x).
        AtualizarSugestaoTitulo();
    }

    // Sem lógica própria — só força StateHasChanged em ProvaForm pra
    // repropagar o valor mudado a componentes irmãos (ex.: card Resumo).
    private void NotificarMudanca()
    {
    }

    // Se trocar o Curso deixar a Turma já escolhida inconsistente, desmarca a Turma.
    private async Task AoMudarCurso()
    {
        if (modelo.TurmaId != 0)
        {
            var turma = todasTurmas.FirstOrDefault(t => t.Id == modelo.TurmaId);
            if (turma is not null && turma.CursoId != modelo.CursoId)
            {
                modelo.TurmaId = 0;
            }
        }

        // Trocar (ou limpar) o Curso invalida item de matriz e Matriz de
        // Referência já marcados no gerador — eles são do curso ANTERIOR.
        gerador.ItemMatrizIdsDesejados.Clear();
        gerador.PercPorItem.Clear();
        gerador.ModoAlinhamentoCurricular = ModoAlinhamentoCurricular.SemMatriz;

        modelo.MatrizReferenciaId = null;
        await CarregarMatrizesDoCursoAsync();
        await CarregarItensDoCursoProvaAsync();

        // Só no modo Curso o pool de candidatas depende do CursoId.
        if (modelo.TipoEscopo == TipoEscopoProva.Curso)
        {
            await RecarregarPoolAsync();
        }

        AtualizarSugestaoTitulo();
    }

    // Escopado à Matriz específica quando há uma escolhida (nunca mistura
    // itens de matrizes diferentes); sem Matriz, usa todos os do Curso.
    private async Task CarregarItensDoCursoProvaAsync()
    {
        itensCurricularProva = modelo.MatrizReferenciaId is int matrizId
            ? await MatrizService.ListarItensAsync(matrizId)
            : modelo.CursoId == 0
                ? new()
                : await MatrizService.ListarItensVinculaveisPorCursoAsync(modelo.CursoId);
    }

    // Reaproveita MatrizReferenciaService.ListarVinculaveisPorCursoAsync, já
    // IDOR-safe, mesma consulta que MatrizForm/MatrizItensPage usam.
    private async Task CarregarMatrizesDoCursoAsync()
    {
        matrizesDoCurso = modelo.CursoId == 0
            ? new()
            : await MatrizService.ListarVinculaveisPorCursoAsync(modelo.CursoId);
    }

    // Trocar a Matriz invalida os itens já marcados no gerador (mesma lógica
    // de AoMudarCurso) e recarrega o pool pro card de diagnóstico refleti-la.
    private async Task AoMudarMatrizAsync()
    {
        gerador.ItemMatrizIdsDesejados.Clear();
        gerador.PercPorItem.Clear();
        gerador.ModoAlinhamentoCurricular = ModoAlinhamentoCurricular.SemMatriz;
        await CarregarItensDoCursoProvaAsync();
        await RecarregarPoolAsync();
    }

    // Marcar/desmarcar disciplina no Multidisciplinar só refina o pool dentro
    // do mesmo modo — não descarta a seleção já feita.
    private async Task AoMudarComposicaoEscopoAsync() => await RecarregarPoolAsync();

    // Trocar o TIPO de escopo é tão disruptivo quanto trocar de Disciplina —
    // mesmo aviso e mesmo "desfazer" via tipoEscopoAnterior.
    private async Task AoMudarEscopoAsync()
    {
        if (!await ConfirmarLimpezaSelecaoAsync())
        {
            modelo.TipoEscopo = tipoEscopoAnterior;
            return;
        }

        tipoEscopoAnterior = modelo.TipoEscopo;
        modelo.DisciplinaId = 0;
        modelo.DisciplinaIds.Clear();
        modelo.DistribuicaoDisciplinas.Clear();
        modelo.MatrizReferenciaId = null;
        modo = ModoMontagem.NaoEscolhido;

        await CarregarItensDoCursoProvaAsync();
        await CarregarQuestoesDisponiveisAsync();
        AtualizarSugestaoTitulo();
    }

    // Gate que decide se já dá pra mostrar "Como deseja montar a prova?".
    // Espelha EscopoProvaConfig.Validar (só forma, sem ida ao banco).
    private bool EscopoPossuiSelecaoValida => modelo.TipoEscopo switch
    {
        TipoEscopoProva.Disciplina => modelo.DisciplinaId != 0,
        TipoEscopoProva.Multidisciplinar => modelo.DisciplinaIds.Count >= 2,
        TipoEscopoProva.Curso => modelo.CursoId != 0,
        _ => false,
    };

    // Contagem real, ao vivo, sobre "selecionadas" — reflete ajustes manuais.
    // Só relevante fora do modo Disciplina.
    private Dictionary<int, string> CalcularDistribuicaoPorDisciplina()
    {
        if (modelo.TipoEscopo == TipoEscopoProva.Disciplina || selecionadas.Count == 0)
        {
            return new();
        }

        var porDisciplina = selecionadas
            .Select(s => questoesPorId.GetValueOrDefault(s.QuestaoId))
            .Where(q => q?.Assunto is not null)
            .GroupBy(q => q!.Assunto!.DisciplinaId)
            .ToDictionary(g => g.Key, g => g.Count());

        var total = selecionadas.Count;
        return porDisciplina.ToDictionary(
            kv => kv.Key,
            kv => $"{disciplinas.FirstOrDefault(d => d.Id == kv.Key)?.Nome ?? $"disciplina #{kv.Key}"} — {kv.Value} ({(int)Math.Round(100.0 * kv.Value / total)}%)");
    }

    private void AplicarFiltro()
    {
        questoesFiltradas = filtroAssuntoId == 0
            ? questoesDisponiveis
            : questoesDisponiveis.Where(q => q.AssuntoId == filtroAssuntoId).ToList();
    }

    private void Adicionar(Questao q)
    {
        if (selecionadas.Any(s => s.QuestaoId == q.Id))
        {
            return;
        }
        questoesPorId[q.Id] = q;
        selecionadas.Add(new QuestaoSelecionada
        {
            QuestaoId = q.Id,
            Ordem = selecionadas.Count == 0 ? 0 : selecionadas.Max(s => s.Ordem) + 1,
        });
    }

    // A questão já está inteira em memória, então abrir o modal é só trocar
    // o ponteiro, sem ida ao banco.
    private void AbrirPreview(Questao q) => questaoPreview = q;

    private void FecharPreview() => questaoPreview = null;

    private void AdicionarDoPreview(Questao q)
    {
        Adicionar(q);
        FecharPreview();
    }

    // Helpers do modal "Visualizar" e DificuldadeEmoji foram movidos pra
    // QuestaoPreviewModal.razor e BancoQuestoesSelector.razor.

    private void Remover(QuestaoSelecionada s) => selecionadas.Remove(s);

    // "arrastando" guarda quem começou o arrasto; ao soltar, a Ordem de
    // TODOS é renumerada sequencialmente (0..n-1), sem risco de colisão.
    private QuestaoSelecionada? arrastando;

    private void IniciarArrasto(QuestaoSelecionada s) => arrastando = s;

    private void SoltarSobre(QuestaoSelecionada alvo)
    {
        if (arrastando is null || ReferenceEquals(arrastando, alvo))
        {
            arrastando = null;
            return;
        }

        var ordenadas = selecionadas.OrderBy(x => x.Ordem).ToList();
        ordenadas.Remove(arrastando);
        var indiceAlvo = ordenadas.IndexOf(alvo);
        ordenadas.Insert(indiceAlvo, arrastando);

        for (var i = 0; i < ordenadas.Count; i++)
        {
            ordenadas[i].Ordem = i;
        }

        arrastando = null;
    }

    private string? avisoSubstituicao;

    // Troca só essa questão por outra do banco com mesmo Assunto, Dificuldade
    // e Bloom; a escolha em si é de GeradorProvaService.EscolherSubstituta.
    private void Substituir(QuestaoSelecionada s)
    {
        avisoSubstituicao = null;
        var atual = questoesPorId[s.QuestaoId];
        var idsNaProva = selecionadas.Select(x => x.QuestaoId).ToHashSet();

        var escolhida = GeradorProvaService.EscolherSubstituta(questoesDisponiveis, atual, idsNaProva, usosPorQuestaoDisponiveis);
        if (escolhida is null)
        {
            avisoSubstituicao = $"Nenhuma questão equivalente disponível pra substituir \"{Truncar(atual.Enunciado)}\" (mesmo assunto, dificuldade e Bloom).";
            return;
        }

        questoesPorId[escolhida.Id] = escolhida;
        s.QuestaoId = escolhida.Id;
    }

    // AbreviacaoTipo foi movido pra QuestoesSelecionadas.razor.

    // "Gerar prova" descarta o que já estava escolhido; "+ Adicionar pelo
    // gerador" mantém e só completa — dois botões pra não surpreender o professor.
    private Task GerarProvaAsync() => SortearQuestoesAsync(substituir: true);

    private Task AdicionarPeloGeradorAsync() => SortearQuestoesAsync(substituir: false);

    private async Task SortearQuestoesAsync(bool substituir)
    {
        avisoGerador = null;

        if (substituir)
        {
            selecionadas.Clear();
        }

        // O motor de geração é orquestrado por ProvaService.GerarAsync; este
        // método é só um wrapper fino. Chamar de novo aqui é defesa extra, idempotente.
        SincronizarEscopoGerador();

        var idsJaSelecionados = selecionadas.Select(s => s.QuestaoId).ToHashSet();
        var resultado = await ProvaService.GerarAsync(gerador, meuId, minhaInstituicaoId, idsJaSelecionados);

        foreach (var q in resultado.Questoes)
        {
            questoesPorId[q.Id] = q;
            selecionadas.Add(new QuestaoSelecionada
            {
                QuestaoId = q.Id,
                Ordem = selecionadas.Count == 0 ? 0 : selecionadas.Max(s => s.Ordem) + 1,
            });
        }

        if (gerador.ValorTotal is > 0 && selecionadas.Count > 0)
        {
            AplicarDistribuicaoValor(gerador.ValorTotal.Value);
        }

        avisoGerador = resultado.Aviso;
    }

    // Valida e delega pro modo escolhido; Manual não chama distribuição nenhuma.
    private void AplicarPontuacao()
    {
        avisoPontuacao = null;

        if (modoPontuacao == ModoPontuacao.Manual)
        {
            return;
        }

        if (selecionadas.Count == 0)
        {
            avisoPontuacao = "Adicione questões antes de distribuir o valor.";
            return;
        }

        if (gerador.ValorTotal is null or <= 0)
        {
            avisoPontuacao = "Informe o valor da prova.";
            return;
        }

        AplicarDistribuicaoValor(gerador.ValorTotal.Value);
    }

    // Ponto único que decide como distribuir o valor, usado tanto pelo botão
    // "Aplicar" quanto automaticamente ao gerar, pra nunca divergir.
    private void AplicarDistribuicaoValor(decimal valorTotal)
    {
        switch (modoPontuacao)
        {
            case ModoPontuacao.ProporcionalDificuldade:
                DistribuirValorProporcionalDificuldade(valorTotal);
                break;
            case ModoPontuacao.Manual:
                // Nada a fazer — distribuição automática desligada de propósito.
                break;
            default:
                DistribuirValorEntreSelecionadas(valorTotal);
                break;
        }
    }

    // Pesos fixos (não configuráveis ainda): Difícil vale o dobro de Fácil,
    // Média fica no meio — arbitrário mas razoável como padrão.
    private static readonly Dictionary<Dificuldade, decimal> pesosDificuldadePontuacao = new()
    {
        [Dificuldade.Facil] = 1m,
        [Dificuldade.Media] = 1.5m,
        [Dificuldade.Dificil] = 2m,
    };

    // Cada questão recebe valorPorPeso * seu peso de Dificuldade, em vez de
    // uma fatia igual (a última absorve o resto do arredondamento).
    private void DistribuirValorProporcionalDificuldade(decimal valorTotal)
    {
        if (selecionadas.Count == 0)
        {
            return;
        }

        var ordenadas = selecionadas.OrderBy(s => s.Ordem).ToList();
        var pesoTotal = ordenadas.Sum(s => pesosDificuldadePontuacao.GetValueOrDefault(questoesPorId[s.QuestaoId].Dificuldade, 1m));
        if (pesoTotal <= 0)
        {
            return;
        }

        var valorPorPeso = valorTotal / pesoTotal;
        var restante = valorTotal;
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var peso = pesosDificuldadePontuacao.GetValueOrDefault(questoesPorId[ordenadas[i].QuestaoId].Dificuldade, 1m);
            var valor = i == ordenadas.Count - 1 ? restante : Math.Round(valorPorPeso * peso, 2);
            ordenadas[i].Valor = valor;
            restante -= valor;
        }
    }

    // Divide igualmente; a última questão absorve o resto do arredondamento.
    // Sempre editável depois — só preenche um valor inicial.
    private void DistribuirValorEntreSelecionadas(decimal valorTotal)
    {
        if (selecionadas.Count == 0)
        {
            return;
        }

        var valorPorQuestao = Math.Round(valorTotal / selecionadas.Count, 2);
        var restante = valorTotal;
        for (var i = 0; i < selecionadas.Count; i++)
        {
            var valor = i == selecionadas.Count - 1 ? restante : valorPorQuestao;
            selecionadas[i].Valor = valor;
            restante -= valor;
        }
    }

    private static string Truncar(string texto) => texto.Length > 70 ? texto[..70] + "..." : texto;

    private bool salvando;

    private async Task SalvarAsync()
    {
        if (salvando)
        {
            return;
        }

        erro = null;

        salvando = true;
        try
        {
            if (Id is null)
            {
                await ProvaService.CriarAsync(modelo, selecionadas, meuId, minhaInstituicaoId);
            }
            else
            {
                await ProvaService.AtualizarAsync(Id.Value, modelo, selecionadas, meuId, minhaInstituicaoId);
            }

            NavigationManager.NavigateTo($"provas?msg={Uri.EscapeDataString($"\"{modelo.Titulo}\" salva com sucesso.")}");
        }
        catch (OperacaoInvalidaException ex)
        {
            erro = ex.Message;
        }
        finally
        {
            salvando = false;
        }
    }
}
