using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa RascunhoQuestaoIaService: ponte simples (sem banco) entre a geração
// por IA e o formulário de Nova Questão — Consumir() só devolve o rascunho
// uma vez e depois some.
public class RascunhoQuestaoIaServiceTests
{
    [Fact]
    public void Consumir_SemDefinirAntes_RetornaNulo()
    {
        var service = new RascunhoQuestaoIaService();

        Assert.Null(service.Consumir());
    }

    [Fact]
    public void Consumir_AposDefinir_RetornaRascunhoEDisciplinaId()
    {
        var service = new RascunhoQuestaoIaService();
        var rascunho = new QuestaoInput { AssuntoId = 42 };

        service.Definir(rascunho, disciplinaId: 7);
        var resultado = service.Consumir();

        Assert.NotNull(resultado);
        Assert.Same(rascunho, resultado.Value.Rascunho);
        Assert.Equal(7, resultado.Value.DisciplinaId);
    }

    [Fact]
    public void Consumir_ChamadoDuasVezes_SoDevolveNaPrimeira()
    {
        var service = new RascunhoQuestaoIaService();
        service.Definir(new QuestaoInput(), disciplinaId: 1);

        var primeiro = service.Consumir();
        var segundo = service.Consumir();

        Assert.NotNull(primeiro);
        Assert.Null(segundo);
    }

    [Fact]
    public void Definir_ChamadoDeNovo_SubstituiORascunhoAnterior()
    {
        var service = new RascunhoQuestaoIaService();
        service.Definir(new QuestaoInput { AssuntoId = 1 }, disciplinaId: 1);
        var segundoRascunho = new QuestaoInput { AssuntoId = 2 };

        service.Definir(segundoRascunho, disciplinaId: 2);
        var resultado = service.Consumir();

        Assert.Same(segundoRascunho, resultado!.Value.Rascunho);
        Assert.Equal(2, resultado.Value.DisciplinaId);
    }
}
