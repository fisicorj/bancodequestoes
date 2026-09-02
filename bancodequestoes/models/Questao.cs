namespace BancoQuestoes.Models;

// "abstract" significa que essa classe não pode ser instanciada diretamente
// (você nunca vai escrever "new Questao()"). Ela existe só para ser herdada.
// Isso faz sentido aqui: toda questão real É uma QuestaoMultiplaEscolha,
// uma QuestaoDiscursiva ou uma QuestaoCertoErrado — "Questao" pura não existe no mundo real.
//
// No banco, com o mapeamento TPT que vamos configurar no DbContext, isso vira
// uma tabela "Questoes" com os campos comuns, e uma tabela extra para cada
// subtipo (QuestoesMultiplaEscolha, QuestoesDiscursivas, QuestoesCertoErrado),
// ligadas por uma FK que também é PK (relação 1-para-1 "é um").
public abstract class Questao
{
    public int Id { get; set; }

    public int AssuntoId { get; set; }
    public Assunto? Assunto { get; set; }

    // Curso ao qual a questão está alinhada — opcional (nulo = questão sem
    // Alinhamento Curricular definido, comportamento de sempre). Diferente
    // de AssuntoId acima, não existia antes desse recurso: Disciplina não
    // pertence a um Curso específico neste sistema (uma mesma Disciplina
    // pode ser oferecida em vários Cursos via Turma), então não dava pra
    // inferir o Curso da questão a partir do Assunto. As diretrizes
    // disponíveis pra vincular (ver Diretrizes abaixo) são sempre as do
    // Curso escolhido aqui — mesmo padrão opcional de Prova.CursoId.
    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    public required string Enunciado { get; set; }

    public TipoQuestao TipoQuestao { get; set; }
    public Dificuldade Dificuldade { get; set; } = Dificuldade.Media;

    public string? CriadoPorId { get; set; }
    public ApplicationUser? CriadoPor { get; set; }

    // Modelo híbrido: Privada (só quem criou vê), Institucional (professores
    // da mesma instituição do criador, ver ApplicationUser.InstituicaoId) ou
    // Compartilhada (qualquer professor do sistema). Ver o filtro central em
    // QuestaoVisibilidade.AplicarFiltro.
    public VisibilidadeQuestao Visibilidade { get; set; } = VisibilidadeQuestao.Privada;

    // Nível cognitivo (Bloom) — nulo quando a questão ainda não foi
    // classificada. Ver o enum NivelBloom pra mais contexto.
    public NivelBloom? Bloom { get; set; }

    // De onde a questão veio, e metadados livres relacionados (ano da prova
    // original, referência do livro/fonte). Ano e Referencia só fazem sentido
    // pra algumas origens (ex.: Livro, ENADE, Concurso) — por isso opcionais,
    // não travados por Origem.
    public OrigemQuestao Origem { get; set; } = OrigemQuestao.Autoral;
    public int? Ano { get; set; }
    public string? Referencia { get; set; }

    // Muitos-para-muitos com Tag (ver ApplicationDbContext.OnModelCreating) —
    // tags livres que o professor usa pra organizar/filtrar o próprio banco de
    // questões (ex.: "pipeline", "cache", "risc").
    public List<Tag> Tags { get; set; } = new();

    // Alinhamento Curricular / ENADE — quais itens de quais Matrizes de
    // Referência (ver MatrizReferencia/ItemMatrizReferencia) essa questão
    // avalia. Muitos-para-muitos via QuestaoItemMatriz (ver
    // ApplicationDbContext.OnModelCreating) — mesmo espírito de Tags acima,
    // só que com uma classe de junção própria (QuestaoItemMatriz), não
    // implícita, porque essa relação tem mais chance de ganhar campos
    // próprios no futuro (ver comentário em QuestaoItemMatriz). Uma questão
    // pode ter itens de MAIS DE UMA matriz ao mesmo tempo (ex.: um item do
    // ENADE 2023 e um item do PPC institucional) — nada aqui limita a uma
    // única matriz. Só faz sentido ter algo aqui quando CursoId está
    // definido — mas o sistema não IMPEDE o contrário; CriarAsync/
    // AtualizarAsync é quem garante consistência (ver QuestaoService).
    public List<ItemMatrizReferencia> ItensMatriz { get; set; } = new();

    // DateTime.UtcNow como valor padrão: sempre grave datas em UTC no banco,
    // e converta para o fuso local só na hora de exibir na tela. Evita bugs
    // de horário quando o servidor e o usuário estão em fusos diferentes.
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }

    // Soft delete: em vez de fazer DELETE no banco, marcamos Ativa = false.
    // Isso preserva o histórico (por exemplo, se a questão já foi usada
    // numa prova antiga, você não perde a referência) e permite "restaurar"
    // uma questão apagada por engano.
    public bool Ativa { get; set; } = true;

    public List<QuestaoImagem> Imagens { get; set; } = new();

    // Resolução/comentário do professor sobre por que a resposta é aquela —
    // opcional, vale pra qualquer tipo de questão (diferente de
    // QuestaoDiscursiva.CriterioAvaliacao, que é a RUBRICA de correção só de
    // discursivas, não uma explicação da resposta certa). Alimenta o
    // "gabarito comentado" (ver Exportacao/ProvaDocxExporter.GerarGabaritoComentado)
    // e entra como critério no farol de qualidade (ver Services/QualidadeQuestao).
    public string? Explicacao { get; set; }
}
