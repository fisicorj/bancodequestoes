namespace BancoQuestoes.Models;

// Abstract: toda questão real É um subtipo concreto (MultiplaEscolha, Discursiva etc.).
// Mapeamento TPT no DbContext: tabela "Questoes" com campos comuns + uma tabela por subtipo.
public abstract class Questao
{
    public int Id { get; set; }

    public int AssuntoId { get; set; }
    public Assunto? Assunto { get; set; }

    // Áreas nacionais às quais a questão é academicamente aplicável — nunca controle de
    // acesso: Visibilidade decide quem vê, isto só decide candidatura de conteúdo.
    public List<AreaCurso> AreasCurso { get; set; } = new();

    public required string Enunciado { get; set; }

    public TipoQuestao TipoQuestao { get; set; }
    public Dificuldade Dificuldade { get; set; } = Dificuldade.Media;

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    // Privada (só quem criou), Institucional (mesma instituição) ou Compartilhada
    // (todo mundo) — ver o filtro central em QuestaoVisibilidade.VisivelPara.
    public VisibilidadeQuestao Visibilidade { get; set; } = VisibilidadeQuestao.Privada;

    // Nulo quando a questão ainda não foi classificada.
    public NivelBloom? Bloom { get; set; }

    // De onde a questão veio. Ano/Referencia só fazem sentido pra algumas origens
    // (Livro, ENADE, Concurso) — por isso opcionais, não travados por Origem.
    public OrigemQuestao Origem { get; set; } = OrigemQuestao.Autoral;
    public int? Ano { get; set; }
    public string? Referencia { get; set; }

    // Formação Geral (D1) ou Componente Específico (D2), só relevante quando Origem ==
    // Enade (validado em QuestaoCurricularService.ValidarConsistenciaEnadeAsync).
    public SecaoEnade? SecaoEnade { get; set; }

    // String, não int: provas ENADE numeram discursivas como "D1"/"D2" ao lado das
    // numéricas "01".."40", e int perderia essa informação.
    public string? NumeroOriginal { get; set; }

    // Código do caderno/prova (ex.: tipo/cor no ENADE) — não confundir com
    // Referencia (citação livre de livro/fonte, sem estrutura).
    public string? CodigoProvaOrigem { get; set; }

    // Tags livres que o professor usa pra organizar/filtrar o próprio banco.
    public List<Tag> Tags { get; set; } = new();

    // Itens de Matrizes que a questão avalia (pode ter de mais de uma matriz). Item
    // NACIONAL exige a AreaCurso em AreasCurso; INSTITUCIONAL só de um Curso por questão.
    public List<ItemMatrizReferencia> ItensMatriz { get; set; } = new();

    // Sempre grava em UTC; converte pro fuso local só na hora de exibir.
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }

    // Soft delete: preserva histórico (questão usada numa prova antiga) e
    // permite restaurar uma questão apagada por engano.
    public bool Ativa { get; set; } = true;

    public List<QuestaoImagem> Imagens { get; set; } = new();

    // Explicação da resposta certa (diferente de QuestaoDiscursiva.CriterioAvaliacao, a
    // rubrica de correção). Alimenta o gabarito comentado e o farol de qualidade.
    public string? Explicacao { get; set; }
}
