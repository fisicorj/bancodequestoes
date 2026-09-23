using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testes de forma (Validar/ParaEscopoQuestoes), puros, sem banco; validação de
// conteúdo é testada em ProvaServiceEscopoTests, contra InMemory.
public class EscopoProvaConfigTests
{
    [Fact]
    public void Validar_Disciplina_SemDisciplinaSelecionada_RetornaErro()
    {
        var escopo = new EscopoProvaConfig { Tipo = TipoEscopoProva.Disciplina };

        Assert.NotNull(escopo.Validar());
    }

    [Fact]
    public void Validar_Disciplina_ComUmaDisciplina_NaoRetornaErro()
    {
        var escopo = new EscopoProvaConfig
        {
            Tipo = TipoEscopoProva.Disciplina,
            DisciplinaIds = new HashSet<int> { 1 },
        };

        Assert.Null(escopo.Validar());
    }

    // Multidisciplinar com menos de 2 disciplinas deve ser rejeitado, nunca
    // tratado silenciosamente como modo Disciplina.
    [Fact]
    public void Validar_Multidisciplinar_ComMenosDeDuasDisciplinas_RetornaErro()
    {
        var escopo = new EscopoProvaConfig
        {
            Tipo = TipoEscopoProva.Multidisciplinar,
            DisciplinaIds = new HashSet<int> { 1 },
        };

        Assert.NotNull(escopo.Validar());
    }

    [Fact]
    public void Validar_Multidisciplinar_ComDuasOuMaisDisciplinas_NaoRetornaErro()
    {
        var escopo = new EscopoProvaConfig
        {
            Tipo = TipoEscopoProva.Multidisciplinar,
            DisciplinaIds = new HashSet<int> { 1, 2 },
        };

        Assert.Null(escopo.Validar());
    }

    [Fact]
    public void Validar_Curso_SemCursoId_RetornaErro()
    {
        var escopo = new EscopoProvaConfig { Tipo = TipoEscopoProva.Curso };

        Assert.NotNull(escopo.Validar());
    }

    [Fact]
    public void Validar_Curso_ComCursoId_NaoRetornaErro()
    {
        var escopo = new EscopoProvaConfig { Tipo = TipoEscopoProva.Curso, CursoId = 7 };

        Assert.Null(escopo.Validar());
    }

    // Modo Curso nunca infere pool por Disciplina — ParaEscopoQuestoes zera
    // DisciplinaIds nesse modo mesmo que tenham vindo preenchidas.
    [Fact]
    public void ParaEscopoQuestoes_ModoCurso_ZeraDisciplinaIds_MesmoSePreenchidas()
    {
        var escopo = new EscopoProvaConfig
        {
            Tipo = TipoEscopoProva.Curso,
            CursoId = 7,
            DisciplinaIds = new HashSet<int> { 1, 2 },
            MatrizReferenciaId = 99,
        };

        var resultado = escopo.ParaEscopoQuestoes();

        Assert.Equal(7, resultado.CursoId);
        Assert.Empty(resultado.DisciplinaIds);
        Assert.Equal(99, resultado.MatrizReferenciaId);
    }

    [Fact]
    public void ParaEscopoQuestoes_ModoMultidisciplinar_PreservaDisciplinaIds_ENaoUsaCursoId()
    {
        var escopo = new EscopoProvaConfig
        {
            Tipo = TipoEscopoProva.Multidisciplinar,
            CursoId = 7,
            DisciplinaIds = new HashSet<int> { 1, 2 },
        };

        var resultado = escopo.ParaEscopoQuestoes();

        Assert.Null(resultado.CursoId);
        Assert.Equal(new HashSet<int> { 1, 2 }, resultado.DisciplinaIds);
    }

    // Tradutor único ProvaInput -> EscopoProvaConfig; testa os três modos,
    // garantindo que cada um só popula os campos que fazem sentido pra ele.
    [Fact]
    public void DoProvaInput_ModoDisciplina_UsaDisciplinaIdUnico()
    {
        var modelo = new ProvaInput { TipoEscopo = TipoEscopoProva.Disciplina, DisciplinaId = 5 };

        var escopo = EscopoProvaConfig.DoProvaInput(modelo);

        Assert.Equal(TipoEscopoProva.Disciplina, escopo.Tipo);
        Assert.Equal(new HashSet<int> { 5 }, escopo.DisciplinaIds);
    }

    [Fact]
    public void DoProvaInput_ModoMultidisciplinar_UsaDisciplinaIds()
    {
        var modelo = new ProvaInput
        {
            TipoEscopo = TipoEscopoProva.Multidisciplinar,
            DisciplinaId = 0,
            DisciplinaIds = new HashSet<int> { 1, 2, 3 },
        };

        var escopo = EscopoProvaConfig.DoProvaInput(modelo);

        Assert.Equal(new HashSet<int> { 1, 2, 3 }, escopo.DisciplinaIds);
    }

    [Fact]
    public void DoProvaInput_ModoCurso_DisciplinaIdsVazio_MesmoComDisciplinaIdAntigoPreenchido()
    {
        // Simula resquício de tela (campo antigo não limpo); nunca deve vazar
        // DisciplinaId pro modo Curso.
        var modelo = new ProvaInput { TipoEscopo = TipoEscopoProva.Curso, DisciplinaId = 5, CursoId = 9 };

        var escopo = EscopoProvaConfig.DoProvaInput(modelo);

        Assert.Empty(escopo.DisciplinaIds);
        Assert.Equal(9, escopo.CursoId);
    }
}
