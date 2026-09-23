using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes da tarefa "Ajuste final — Vínculos históricos de itens de matriz".
//
// O problema corrigido era só de UI: QuestaoForm.razor/
// QuestaoAlinhamentoCurricularSection.razor montavam o checklist visível só
// com ItensDoCurso/ItensDasAreas (MatrizReferenciaService.
// ListarItensVinculaveisPorCursoAsync/PorAreasCursoAsync), que já filtram
// Ativo/Status=Rascunho de propósito (são "o que dá pra OFERECER agora") —
// então um item que a questão já tinha vinculado ANTES de virar inativo/
// Rascunho sumia da tela, mesmo o backend preservando o vínculo
// corretamente. Sem conseguir VER o item, o professor não tinha como
// desmarcá-lo explicitamente, e os bloqueios de Correção 1 ("Ajustes finais
// de integridade curricular") ficavam travados sem solução na tela.
//
// A correção foi só de apresentação (novo conjunto ItensHistoricos, união
// sem duplicar, aviso "vínculo histórico" em ItemMatrizChecklist) — o
// backend (QuestaoCurricularService.ResolverItensMatrizAsync, com o parâmetro
// idsJaVinculadosAntes já existente) não mudou nada. Os testes abaixo
// cobrem os 7 cenários pedidos; como este projeto não tem bUnit (só testes
// de Service — ver comentário em bancodequestoes.Tests.csproj), os
// cenários que são puramente sobre RENDERIZAÇÃO (o aviso aparecer, o
// checkbox continuar clicável) foram verificados por leitura de código. O
// que dá pra testar automaticamente — e é testado aqui — é a CAMADA DE
// DADOS que alimenta essa renderização (o vínculo continuar em
// ItensMatriz/Ativo=false após reload, o backend aceitar a remoção
// explícita, o backend rejeitar a readição) e a fórmula de deduplicação em
// si (cenário 7), reproduzida aqui exatamente como está em
// QuestaoForm.ItensHistoricos/QuestaoAlinhamentoCurricularSection.
// ItensVisiveis.
public class AjusteVinculosHistoricosItensMatrizTests
{
    private static QuestaoService NovoService(BancoQuestoes.Data.ApplicationDbContext db) =>
        TestFakes.NovoQuestaoService(db);

    private static async Task<(Disciplina Disciplina, Assunto Assunto)> SeedDisciplinaAssuntoAsync(BancoQuestoes.Data.ApplicationDbContext db)
    {
        var disciplina = TestSeed.Disciplina("Redes");
        db.Disciplinas.Add(disciplina);
        await db.SaveChangesAsync();
        var assunto = TestSeed.Assunto("Roteamento", disciplina.Id);
        db.Assuntos.Add(assunto);
        await db.SaveChangesAsync();
        return (disciplina, assunto);
    }

    private static QuestaoInput ModeloBase(int assuntoId) => new()
    {
        AssuntoId = assuntoId,
        Enunciado = "Questão de teste",
        Alternativas = new List<AlternativaInput> { new() { Texto = "A" }, new() { Texto = "B" } },
    };

    // Cenário 1 — item ATIVO associado continua aparecendo normalmente: cria,
    // resalva sem tocar em nada, o vínculo permanece e (pela mesma fórmula
    // que QuestaoForm.ItensHistoricos usa) não seria classificado como
    // histórico.
    [Fact]
    public async Task Cenario1_ItemAtivoAssociado_ContinuaENaoEClassificadoComoHistorico()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id); // Ativo = true
        item.MatrizReferencia = ppc; // fixup explícito — a checagem abaixo lê esta propriedade direto no objeto local, sem recarregar via Include
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { item.Id };
        await service.AtualizarAsync(criada.Id, editarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
        Assert.True(recarregada.ItensMatriz[0].Ativo);

        // Mesma fórmula de QuestaoForm.ItensHistoricos: item ativo, presente
        // em "itensDoCurso" (simulado abaixo) — não deve contar como
        // histórico.
        var itensDoCursoSimulado = new List<ItemMatrizReferencia> { item };
        var historico = new[] { item }
            .Where(i => !i.Ativo || i.MatrizReferencia is null || !i.MatrizReferencia.PodeSerUtilizadaEmNovoVinculo())
            .Where(i => !itensDoCursoSimulado.Any(x => x.Id == i.Id))
            .ToList();
        Assert.Empty(historico);
    }

    // Cenário 2 — item associado é posteriormente inativado: continua
    // vinculado (dado que a UI usa pra desenhar o "☑ ... vínculo histórico")
    // e o vínculo é preservado se o professor não mexer nele (resave normal).
    [Fact]
    public async Task Cenario2_ItemInativadoDepoisDeAssociado_VinculoPreservadoEIdentificavelComoHistorico()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        item.Ativo = false;
        await db.SaveChangesAsync();

        // "continua visível na edição": o vínculo (QuestaoItemMatriz) segue
        // presente em ItensMatriz mesmo com o item inativo — é essa
        // informação que QuestaoForm.OnInitializedAsync usa pra popular
        // detalhesItensSelecionados/itemMatrizIdsSelecionados e, por tabela,
        // ItensHistoricos.
        var carregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(carregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(carregada!.ItensMatriz);
        Assert.False(carregada.ItensMatriz[0].Ativo); // "identificado como histórico/inativo"

        // "vínculo é preservado se o usuário não mexer nele": resave sem
        // tocar no alinhamento curricular continua aceito e mantém o item.
        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.Enunciado = "Questão de teste (revisada)";
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { item.Id };
        await service.AtualizarAsync(criada.Id, editarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Single(recarregada!.ItensMatriz);
        Assert.Equal(item.Id, recarregada.ItensMatriz[0].Id);
    }

    // Cenário 3 — item histórico inativo é explicitamente desmarcado (a tela
    // simplesmente não envia mais o id em ItemMatrizIds): a associação é
    // removida.
    [Fact]
    public async Task Cenario3_ItemHistoricoDesmarcadoExplicitamente_AssociacaoEhRemovida()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        item.Ativo = false;
        await db.SaveChangesAsync();

        // Professor desmarca o item histórico na tela — ItemMatrizIds chega
        // vazio (nenhum item mais selecionado).
        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int>();
        await service.AtualizarAsync(criada.Id, editarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null);

        var recarregada = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(recarregada!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Empty(recarregada!.ItensMatriz);
    }

    // Cenário 4 — depois de removido, uma requisição POSTERIOR tentando
    // associar de novo o mesmo item (ainda inativo) precisa ser rejeitada.
    [Fact]
    public async Task Cenario4_ReadicionarItemInativoRemovidoAnteriormente_EhRejeitadoPeloBackend()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        item.Ativo = false;
        await db.SaveChangesAsync();

        // Passo 1: remove o vínculo histórico (salva com sucesso).
        var removerModelo = ModeloBase(assunto.Id);
        removerModelo.CursoContextoMatrizId = cursoA.Id;
        removerModelo.ItemMatrizIds = new List<int>();
        await service.AtualizarAsync(criada.Id, removerModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null);

        // Passo 2: requisição SEPARADA e posterior tenta reassociar o MESMO
        // item, que continua inativo — precisa ser rejeitada (não é mais
        // "carryover", porque a questão não tem mais esse vínculo).
        var readicionarModelo = ModeloBase(assunto.Id);
        readicionarModelo.CursoContextoMatrizId = cursoA.Id;
        readicionarModelo.ItemMatrizIds = new List<int> { item.Id };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.AtualizarAsync(criada.Id, readicionarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("inativ", ex.Message, StringComparison.OrdinalIgnoreCase);

        var final = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(final!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Empty(final!.ItensMatriz); // continua removido, a tentativa rejeitada não recolou o vínculo
    }

    // Cenário 5 — mesmo comportamento de remoção + rejeição de readição,
    // agora pra matriz que virou Rascunho (em vez do item individual ficar
    // inativo).
    [Fact]
    public async Task Cenario5_MatrizViraRascunho_RemoverEDepoisReadicionar_RemoveEDepoisRejeita()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC); // Status = Ativa
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var item = TestSeed.Item("C01", "Competência 1", ppc.Id);
        db.ItensMatrizReferencia.Add(item);
        await db.SaveChangesAsync();

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { item.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        ppc.Status = StatusMatrizReferencia.Rascunho;
        await db.SaveChangesAsync();

        // Remove o vínculo histórico.
        var removerModelo = ModeloBase(assunto.Id);
        removerModelo.CursoContextoMatrizId = cursoA.Id;
        removerModelo.ItemMatrizIds = new List<int>();
        await service.AtualizarAsync(criada.Id, removerModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null);

        var apósRemocao = await db.Questoes.FindAsync(criada.Id);
        await db.Entry(apósRemocao!).Collection(q => q.ItensMatriz).LoadAsync();
        Assert.Empty(apósRemocao!.ItensMatriz);

        // Tenta readicionar — matriz continua Rascunho, rejeita.
        var readicionarModelo = ModeloBase(assunto.Id);
        readicionarModelo.CursoContextoMatrizId = cursoA.Id;
        readicionarModelo.ItemMatrizIds = new List<int> { item.Id };

        var ex = await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.AtualizarAsync(criada.Id, readicionarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null));
        Assert.Contains("Rascunho", ex.Message);
    }

    // Cenário 6 — item inativo/Rascunho que NUNCA esteve associado à questão:
    // não aparece como opção (MatrizReferenciaService.
    // ListarItensVinculaveisPorCursoAsync/PorAreasCursoAsync, que alimentam
    // itensDoCurso/itensDasAreas, já filtram Ativo/Status — testado à parte
    // em AjustesFinaisIntegridadeCurricularTests) e o backend rejeita
    // qualquer tentativa manual de associá-lo, inclusive editando uma
    // questão já existente (que tem OUTRO item, ativo, no meio).
    [Fact]
    public async Task Cenario6_ItemInativoNuncaAssociado_NaoApareceComoOpcaoEBackendRejeitaAoEditar()
    {
        using var db = TestDbFactory.Criar();

        var cursoA = TestSeed.Curso("Engenharia de Computação", instituicaoId: 1);
        db.Cursos.Add(cursoA);
        await db.SaveChangesAsync();
        var ppc = TestSeed.Matriz("PPC 2024", cursoA.Id, TipoMatrizReferencia.PPC);
        db.MatrizesReferencia.Add(ppc);
        await db.SaveChangesAsync();
        var itemAtivo = TestSeed.Item("C01", "Competência 1", ppc.Id);
        var itemInativoNuncaUsado = new ItemMatrizReferencia { Codigo = "C99", Titulo = "Nunca usado", MatrizReferenciaId = ppc.Id, Ativo = false };
        db.ItensMatrizReferencia.AddRange(itemAtivo, itemInativoNuncaUsado);
        await db.SaveChangesAsync();

        // "não aparece como opção": a listagem que monta itensDoCurso não
        // devolve o item inativo.
        var matrizService = TestFakes.NovaMatrizReferenciaService(db);
        var ofertados = await matrizService.ListarItensVinculaveisPorCursoAsync(cursoA.Id);
        Assert.Contains(ofertados, i => i.Id == itemAtivo.Id);
        Assert.DoesNotContain(ofertados, i => i.Id == itemInativoNuncaUsado.Id);

        var (_, assunto) = await SeedDisciplinaAssuntoAsync(db);
        var service = NovoService(db);

        var criarModelo = ModeloBase(assunto.Id);
        criarModelo.CursoContextoMatrizId = cursoA.Id;
        criarModelo.ItemMatrizIds = new List<int> { itemAtivo.Id };
        var criada = await service.CriarAsync(criarModelo, new List<PendenteImagem>(), criadoPorId: "prof-1", minhaInstituicaoId: null);

        // "backend rejeita associação manual": editar a questão tentando
        // ADICIONAR o item que nunca foi associado (junto do que já era
        // válido) é rejeitado — não é carryover, é associação nova.
        var editarModelo = ModeloBase(assunto.Id);
        editarModelo.CursoContextoMatrizId = cursoA.Id;
        editarModelo.ItemMatrizIds = new List<int> { itemAtivo.Id, itemInativoNuncaUsado.Id };

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() =>
            service.AtualizarAsync(criada.Id, editarModelo, new List<QuestaoImagem>(), new HashSet<int>(), new List<PendenteImagem>(), meuId: "prof-1", minhaInstituicaoId: null));
    }

    // Cenário 7 — não duplicar item quando ele estiver simultaneamente
    // presente em coleções usadas pra montar a interface. Reproduz aqui a
    // MESMA fórmula (Concat + DistinctBy / dupla checagem de pertencimento)
    // usada em QuestaoForm.ItensHistoricos e QuestaoAlinhamentoCurricular
    // Section.ItensVisiveis — não há como testar o componente .razor em si
    // (sem bUnit no projeto), então isto garante que a fórmula, aplicada a
    // dados realistas, não duplica.
    [Fact]
    public void Cenario7_FormulaDeUniaoDosChecklists_NaoDuplicaItemJaOferecidoESelecionado()
    {
        var matrizPpc = new MatrizReferencia { Id = 1, Nome = "PPC 2024", CursoId = 10, Tipo = TipoMatrizReferencia.PPC, Status = StatusMatrizReferencia.Ativa };
        var itemAtivoJaSelecionado = new ItemMatrizReferencia { Id = 1, Codigo = "C01", Titulo = "Competência 1", Ativo = true, MatrizReferencia = matrizPpc };

        // Situação normal: o item está OFERECIDO (itensDoCurso) e também JÁ
        // SELECIONADO (detalhesItensSelecionados) — o caso mais comum de
        // "professor editando uma questão que já tinha esse item marcado, e
        // ele continua válido".
        var itensDoCurso = new List<ItemMatrizReferencia> { itemAtivoJaSelecionado };
        var itensDasAreas = new List<ItemMatrizReferencia>();
        var detalhesItensSelecionados = new Dictionary<int, ItemMatrizReferencia> { [itemAtivoJaSelecionado.Id] = itemAtivoJaSelecionado };

        // Fórmula de QuestaoForm.ItensHistoricos:
        var itensHistoricos = detalhesItensSelecionados.Values
            .Where(i => !i.Ativo || i.MatrizReferencia is null || !i.MatrizReferencia.PodeSerUtilizadaEmNovoVinculo())
            .Where(i => !itensDoCurso.Any(x => x.Id == i.Id) && !itensDasAreas.Any(x => x.Id == i.Id))
            .ToList();
        Assert.Empty(itensHistoricos); // item ativo e já ofertado não deveria "duplicar" como histórico

        // Fórmula de QuestaoAlinhamentoCurricularSection.ItensVisiveis:
        var itensVisiveis = itensDoCurso.Concat(itensDasAreas).Concat(itensHistoricos).DistinctBy(i => i.Id).ToList();
        Assert.Single(itensVisiveis);

        // Segunda checagem, agora com um item GENUINAMENTE histórico (não
        // ofertado) — confirma que ele entra em ItensHistoricos exatamente
        // uma vez e não duplica ao entrar na união final.
        var itemInativo = new ItemMatrizReferencia { Id = 2, Codigo = "C02", Titulo = "Competência 2", Ativo = false, MatrizReferencia = matrizPpc };
        detalhesItensSelecionados[itemInativo.Id] = itemInativo;

        var itensHistoricos2 = detalhesItensSelecionados.Values
            .Where(i => !i.Ativo || i.MatrizReferencia is null || !i.MatrizReferencia.PodeSerUtilizadaEmNovoVinculo())
            .Where(i => !itensDoCurso.Any(x => x.Id == i.Id) && !itensDasAreas.Any(x => x.Id == i.Id))
            .ToList();
        Assert.Single(itensHistoricos2);
        Assert.Equal(itemInativo.Id, itensHistoricos2[0].Id);

        var itensVisiveis2 = itensDoCurso.Concat(itensDasAreas).Concat(itensHistoricos2).DistinctBy(i => i.Id).ToList();
        Assert.Equal(2, itensVisiveis2.Count);
        Assert.Equal(new[] { 1, 2 }, itensVisiveis2.Select(i => i.Id).OrderBy(id => id));
    }
}
