namespace BancoQuestoes.Services;

// Exceção "esperada": usada pelos Services pra sinalizar uma regra de negócio
// violada (registro não encontrado, exclusão bloqueada por vínculo, permissão
// negada) com uma mensagem já pronta pra mostrar na tela — diferente de uma
// exceção de verdade (bug, falha de infraestrutura), que deve continuar
// subindo sem ser "traduzida". As páginas .razor capturam só este tipo
// específico (nunca Exception genérica) e mostram ex.Message direto no
// `erro`/alerta da tela.
public sealed class OperacaoInvalidaException : Exception
{
    public OperacaoInvalidaException(string mensagem) : base(mensagem)
    {
    }
}
