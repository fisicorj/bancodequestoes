using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Services;

// Resolução/validação do Alinhamento Curricular de uma questão (AreaCurso,
// ItensMatriz) e consistência ENADE/SecaoEnade; chamado só por QuestaoService.
public class QuestaoCurricularService(ApplicationDbContext db, MatrizReferenciaService matrizService)
{
    // Só devolve AreaCurso que REALMENTE existem — ids inativos são aceitos de
    // propósito, pra questão já classificada numa Área desativada não perder o vínculo.
    public async Task<List<AreaCurso>> ResolverAreasCursoAsync(List<int> areaCursoIds)
    {
        var ids = areaCursoIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<AreaCurso>();
        }

        return await db.AreasCurso.Where(a => ids.Contains(a.Id)).ToListAsync();
    }

    // Une as duas validações de item de matriz: aceito se pertencer ao Curso
    // institucional de contexto OU a alguma Área marcada, sempre por rejeição explícita.
    public async Task<List<ItemMatrizReferencia>> ResolverItensMatrizAsync(
        int? cursoContextoMatrizId, List<int> areaCursoIds, List<int> itemMatrizIds, HashSet<int> idsJaVinculadosAntes)
    {
        var idsPedidos = itemMatrizIds.Distinct().ToList();
        if (idsPedidos.Count == 0)
        {
            return new List<ItemMatrizReferencia>();
        }

        var itensPedidos = await db.ItensMatrizReferencia
            .Include(i => i.MatrizReferencia)
            .Where(i => idsPedidos.Contains(i.Id))
            .ToListAsync();

        // Id inexistente rejeita a operação INTEIRA, sem exceção — itens só são
        // desativados, nunca apagados, então um id inexistente é dado corrompido.
        var idsEncontrados = itensPedidos.Select(i => i.Id).ToHashSet();
        var idsNaoEncontrados = idsPedidos.Except(idsEncontrados).ToList();
        if (idsNaoEncontrados.Count > 0)
        {
            throw new OperacaoInvalidaException(
                "Um ou mais itens de matriz selecionados não existem ou não estão disponíveis.");
        }

        // Item inativo ou de matriz Rascunho nunca pode virar associação NOVA —
        // "nova" exclui quem já estava em idsJaVinculadosAntes (vínculo antigo intocado não conta).
        var idsNovos = idsPedidos.Except(idsJaVinculadosAntes).ToHashSet();
        var itensNovosPedidos = itensPedidos.Where(i => idsNovos.Contains(i.Id)).ToList();

        if (itensNovosPedidos.Any(i => !i.Ativo))
        {
            throw new OperacaoInvalidaException(
                "Um ou mais itens de matriz selecionados estão inativos e não podem ser utilizados.");
        }

        if (itensNovosPedidos.Any(i => i.MatrizReferencia is null || !i.MatrizReferencia.PodeSerUtilizadaEmNovoVinculo()))
        {
            // Mesma regra de "matriz utilizável" da UI — Rascunho é o único Status não-utilizável.
            throw new OperacaoInvalidaException(
                "Um ou mais itens pertencem a uma matriz de referência que ainda não está disponível para uso (Rascunho).");
        }

        var areasIds = areaCursoIds.Distinct().ToList();
        var itemNacionalIncompativel = itensPedidos.Any(i =>
            i.MatrizReferencia?.AreaCursoId is int areaDoItem && !areasIds.Contains(areaDoItem));
        if (itemNacionalIncompativel)
        {
            throw new OperacaoInvalidaException(
                "Há item(ns) de matriz nacional (ENADE/DCN) marcado(s) que não pertencem a nenhuma das Áreas de Curso selecionadas. Marque a Área correspondente, ou desmarque o item, antes de salvar.");
        }

        var cursosInstitucionaisPedidos = itensPedidos
            .Where(i => i.MatrizReferencia?.CursoId is not null)
            .Select(i => i.MatrizReferencia!.CursoId!.Value)
            .Distinct()
            .ToList();

        if (cursosInstitucionaisPedidos.Count > 1
            || (cursosInstitucionaisPedidos.Count == 1 && cursosInstitucionaisPedidos[0] != cursoContextoMatrizId))
        {
            throw new OperacaoInvalidaException("Uma questão pode utilizar itens de matriz institucional de apenas um curso por vez.");
        }

        // Os dois Validar* abaixo são defesa em profundidade mas também filtram
        // Ativo/Status — por isso o carryover histórico inativo/Rascunho é somado de volta.
        var itensPorCurso = await matrizService.ValidarItensDoCursoAsync(cursoContextoMatrizId, idsPedidos);
        var itensPorArea = await matrizService.ValidarItensPorAreasCursoAsync(areaCursoIds, idsPedidos);
        var resultado = itensPorCurso.UnionBy(itensPorArea, i => i.Id).ToList();

        var idsNoResultado = resultado.Select(i => i.Id).ToHashSet();
        var carryoverPreservado = itensPedidos.Where(i =>
            idsJaVinculadosAntes.Contains(i.Id) && !idsNoResultado.Contains(i.Id));
        resultado.AddRange(carryoverPreservado);

        return resultado;
    }

    // Garante consistência ENADE/Formação Geral por REJEIÇÃO explícita: Origem
    // Enade exige SecaoEnade; FormacaoGeral exige Disciplina própria e proíbe AreaCursoIds; etc.
    public async Task ValidarConsistenciaEnadeAsync(QuestaoInput modelo)
    {
        if (modelo.Origem == OrigemQuestao.Enade)
        {
            if (modelo.SecaoEnade is null)
            {
                throw new OperacaoInvalidaException("Questões de Origem ENADE precisam informar a Seção (Formação Geral ou Componente Específico).");
            }
        }
        else if (modelo.SecaoEnade is not null)
        {
            throw new OperacaoInvalidaException("Seção do ENADE só se aplica a questões de Origem ENADE.");
        }

        if (modelo.SecaoEnade == BancoQuestoes.Models.SecaoEnade.FormacaoGeral)
        {
            if (modelo.AreaCursoIds.Count > 0)
            {
                throw new OperacaoInvalidaException("Questões de Formação Geral não podem estar vinculadas a Área de Curso.");
            }

            // AssuntoId == 0 já foi rejeitado por QuestaoService.ValidarModelo antes desta checagem.
            var disciplinaId = await db.Assuntos
                .Where(a => a.Id == modelo.AssuntoId)
                .Select(a => a.DisciplinaId)
                .FirstOrDefaultAsync();
            var codigoDisciplina = await db.Disciplinas
                .Where(d => d.Id == disciplinaId)
                .Select(d => d.Codigo)
                .FirstOrDefaultAsync();

            if (codigoDisciplina != Disciplina.CodigoFormacaoGeral)
            {
                throw new OperacaoInvalidaException("Questões de Formação Geral precisam usar a disciplina \"Formação Geral\".");
            }
        }
        else if (modelo.SecaoEnade == BancoQuestoes.Models.SecaoEnade.ComponenteEspecifico)
        {
            if (modelo.AreaCursoIds.Count == 0)
            {
                throw new OperacaoInvalidaException("Questões de Componente Específico (ENADE) precisam de pelo menos uma Área de Curso.");
            }
        }
    }
}
