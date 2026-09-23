namespace BancoQuestoes.Services;

// Exceção "esperada" de regra de negócio, com mensagem pronta pra tela — as
// páginas .razor capturam só este tipo (nunca Exception genérica).
public sealed class OperacaoInvalidaException : Exception
{
    public OperacaoInvalidaException(string mensagem) : base(mensagem)
    {
    }
}
