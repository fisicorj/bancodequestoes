namespace BancoQuestoes.Services;

// Ponte (Service Scoped) entre a geração de questão por IA e o formulário de
// Nova Questão. Consumir() limpa o valor após ler, pra aplicar só uma vez.
public class RascunhoQuestaoIaService
{
    private QuestaoInput? _rascunho;
    private int _disciplinaId;

    public void Definir(QuestaoInput rascunho, int disciplinaId)
    {
        _rascunho = rascunho;
        _disciplinaId = disciplinaId;
    }

    public (QuestaoInput Rascunho, int DisciplinaId)? Consumir()
    {
        if (_rascunho is null)
        {
            return null;
        }

        var resultado = (_rascunho, _disciplinaId);
        _rascunho = null;
        return resultado;
    }
}
