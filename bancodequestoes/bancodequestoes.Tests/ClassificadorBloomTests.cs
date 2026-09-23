using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Xunit;

namespace BancoQuestoes.Tests;

// Testa ClassificadorBloom.Classificar: heurística por palavra-chave do
// enunciado, verificada do nível mais sofisticado (Criar) pro mais básico
// (Lembrar) — a ordem importa quando uma palavra aparece em mais de um nível.
public class ClassificadorBloomTests
{
    [Theory]
    [InlineData("Crie um algoritmo que resolva o problema.", NivelBloom.Criar)]
    [InlineData("Avalie criticamente a solução proposta pelo autor.", NivelBloom.Avaliar)]
    [InlineData("Analise o gráfico e identifique as causas do erro.", NivelBloom.Analisar)]
    [InlineData("Calcule a força resultante sobre o corpo.", NivelBloom.Aplicar)]
    [InlineData("Explique por que a água ferve a 100°C ao nível do mar.", NivelBloom.Entender)]
    [InlineData("Defina o conceito de entropia.", NivelBloom.Lembrar)]
    public void Classificar_ReconheceONivelPelaPalavraChave(string enunciado, NivelBloom esperado)
    {
        Assert.Equal(esperado, ClassificadorBloom.Classificar(enunciado));
    }

    [Fact]
    public void Classificar_SemPalavraChaveConhecida_RetornaNull()
    {
        var resultado = ClassificadorBloom.Classificar("Texto de enunciado sem nenhum verbo de comando reconhecido.");
        Assert.Null(resultado);
    }

    [Fact]
    public void Classificar_EIgnoraCaixaDasLetras()
    {
        Assert.Equal(NivelBloom.Criar, ClassificadorBloom.Classificar("ELABORE um projeto de rede."));
    }

    // "identifique" sozinho é Lembrar, mas a frase "identifique as causas" é
    // tratada como Analisar — checado primeiro por vir antes na ordem de níveis.
    [Fact]
    public void Classificar_FraseMaisEspecificaDeNivelSuperior_TemPrioridadeSobrePalavraIsolada()
    {
        Assert.Equal(NivelBloom.Analisar, ClassificadorBloom.Classificar("Identifique as causas do problema."));
        Assert.Equal(NivelBloom.Lembrar, ClassificadorBloom.Classificar("Identifique os componentes do sistema."));
    }

    // Mesmo padrão com "compare": "compare e contraste" é Analisar,
    // "compare" isolado cai pro Entender (nível mais baixo, checado por último).
    [Fact]
    public void Classificar_CompareEContraste_VersusCompareIsolado()
    {
        Assert.Equal(NivelBloom.Analisar, ClassificadorBloom.Classificar("Compare e contraste os dois métodos."));
        Assert.Equal(NivelBloom.Entender, ClassificadorBloom.Classificar("Compare os dois métodos."));
    }

    [Fact]
    public void Classificar_NaoCasaSubstringDentroDeOutraPalavra()
    {
        // "listagem" contém "liste" como substring, mas \b...\b não deve casar
        // no meio da palavra — não é um comando de Lembrar.
        var resultado = ClassificadorBloom.Classificar("A listagem de arquivos foi gerada automaticamente.");
        Assert.Null(resultado);
    }
}
