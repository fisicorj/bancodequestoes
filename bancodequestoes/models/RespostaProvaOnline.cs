namespace BancoQuestoes.Models;

// Uma tentativa de um Aluno numa AplicacaoProva — única por (AplicacaoProvaId, AlunoId),
// já que só é permitida uma tentativa (regra de negócio confirmada com o professor).
public class RespostaProvaOnline
{
    public int Id { get; set; }

    public int AplicacaoProvaId { get; set; }
    public AplicacaoProva? AplicacaoProva { get; set; }

    public int AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    public StatusRespostaProvaOnline Status { get; set; } = StatusRespostaProvaOnline.EmAndamento;

    public DateTime IniciadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? FinalizadoEm { get; set; }

    // Nulo enquanto EmAndamento; preenchido ao Enviar (normal ou forçado pelo anti-cola).
    public MotivoEncerramento? MotivoEncerramento { get; set; }

    // Soma das PontuacaoObtida de RespostaQuestaoOnline — calculada na correção automática
    // ao Enviar, não recalculada depois (nem quando o professor Libera).
    public decimal? NotaTotal { get; set; }

    public DateTime? LiberadoEm { get; set; }

    public List<RespostaQuestaoOnline> Respostas { get; set; } = new();
}

// Resposta a UMA questão dentro de uma tentativa. Só um dos campos Resposta* é preenchido,
// de acordo com o TipoQuestao da questão referenciada — sem subtipo por tabela (diferente de
// Questao) porque só 4 tipos são auto-corrigíveis em v1 e cada resposta é um valor simples.
public class RespostaQuestaoOnline
{
    public int Id { get; set; }

    public int RespostaProvaOnlineId { get; set; }
    public RespostaProvaOnline? RespostaProvaOnline { get; set; }

    // Restrict no DbContext: histórico de resposta não pode ficar órfão de uma
    // exclusão silenciosa da questão.
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    // MultiplaEscolha
    public char? RespostaMultiplaEscolha { get; set; }

    // CertoErrado
    public bool? RespostaCertoErrado { get; set; }

    // Numerica — entrada não numérica ou em branco fica nula (conta como errada).
    public decimal? RespostaNumerica { get; set; }

    // Lacunas: cada lacuna em RespostasLacunas.
    public List<RespostaLacunaOnline> RespostasLacunas { get; set; } = new();

    // Nulo até a correção automática rodar (no Enviar); depois sempre preenchido pros 4
    // tipos auto-corrigíveis.
    public bool? Correta { get; set; }

    public decimal? PontuacaoObtida { get; set; }
}

// Resposta a UMA lacuna dentro de uma questão de Lacunas — casa por Ordem com
// LacunaResposta.Ordem da questão original, mesmo padrão de comparação por posição.
public class RespostaLacunaOnline
{
    public int Id { get; set; }

    public int RespostaQuestaoOnlineId { get; set; }
    public RespostaQuestaoOnline? RespostaQuestaoOnline { get; set; }

    public int Ordem { get; set; }
    public required string RespostaTexto { get; set; }

    public bool Correta { get; set; }
}
