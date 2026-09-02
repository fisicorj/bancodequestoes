namespace BancoQuestoes.Models;

// Uma matriz de referência curricular (ENADE, DCN, PPC, matriz institucional
// etc.) — não pertence a uma Disciplina, porque uma competência normalmente
// é trabalhada por várias disciplinas do curso ao mesmo tempo (ver
// comentário em Questao.ItensMatriz).
//
// Diferente da modelagem anterior desse recurso (uma única árvore de
// "diretrizes" por curso), aqui existe EXPLICITAMENTE o conceito de versão:
// um mesmo Curso/AreaCurso pode ter "ENADE 2023" (Historica) e "ENADE 2026"
// (Ativa) ao mesmo tempo, cada uma com seus próprios Itens — trocar de
// edição nunca apaga/reescreve a anterior, então questões classificadas com
// a matriz antiga continuam íntegras (ver QuestaoItemMatriz).
//
// ESCOPO DUPLO (2ª rodada de revisão, discussão sobre ENADE ser nacional):
// exatamente UM entre CursoId e AreaCursoId é preenchido, nunca os dois,
// nunca nenhum — validado em MatrizReferenciaService.ValidarMatriz, não no
// banco (mesmo estilo de validação de negócio já usado no resto do
// projeto). Qual dos dois depende do Tipo:
//   - ENADE/DCN (documento nacional do MEC/INEP) -> AreaCursoId. A mesma
//     matriz vale pra qualquer Curso (de qualquer instituição) que aponte
//     pra essa AreaCurso — ver Curso.AreaCursoId.
//   - PPC/Institucional/Outro (documento da própria instituição) -> CursoId,
//     como sempre foi.
public class MatrizReferencia
{
    public int Id { get; set; }

    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    public int? AreaCursoId { get; set; }
    public AreaCurso? AreaCurso { get; set; }

    public required string Nome { get; set; }

    public TipoMatrizReferencia Tipo { get; set; }

    public int? Ano { get; set; }
    public string? Edicao { get; set; }

    // Fonte documental (item de auditoria acadêmica) — nenhum desses quatro
    // é obrigatório porque nem toda matriz (ex.: uma matriz institucional
    // informal) tem necessariamente portaria/URL publicada.
    public string? Orgao { get; set; }
    public string? Documento { get; set; }
    public string? UrlFonte { get; set; }

    public string? Descricao { get; set; }

    public StatusMatrizReferencia Status { get; set; } = StatusMatrizReferencia.Rascunho;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<ItemMatrizReferencia> Itens { get; set; } = new();
}

// Um item de uma matriz — uma competência, um item do perfil do concluinte,
// um conteúdo, uma habilidade etc. Deliberadamente SEM auto-relacionamento
// (diferente do design anterior desse recurso): os exemplos reais de matriz
// (ENADE, DCN) mostram grupos FLAT — "C01, C02, C03..." dentro de
// "Competências", sem sub-itens — então uma hierarquia genérica seria
// complexidade sem uso real aqui. Tipo (ver TipoItemMatriz) já cumpre o
// papel de "agrupar" P01/C01/CT01 nas telas.
public class ItemMatrizReferencia
{
    public int Id { get; set; }

    public int MatrizReferenciaId { get; set; }
    public MatrizReferencia? MatrizReferencia { get; set; }

    public required string Codigo { get; set; }
    public required string Titulo { get; set; }
    public string? Descricao { get; set; }

    public TipoItemMatriz Tipo { get; set; }

    public int Ordem { get; set; }

    // Desativar em vez de apagar preserva o histórico de questões já
    // vinculadas a esse item — mesmo padrão de Questao.Ativa/
    // DiretrizCurricular.Ativo (recurso anterior).
    public bool Ativo { get; set; } = true;
}

// Vínculo muitos-para-muitos entre Questao e ItemMatrizReferencia — uma
// questão pode atender vários itens (de uma ou mais matrizes ao mesmo
// tempo, ver item 10 do pedido), e um item pode ser abordado por várias
// questões. Classe própria (não implícita, como Questao.Tags) pelo mesmo
// motivo do recurso anterior: deixa espaço pra ganhar colunas próprias no
// futuro (peso, origem de uma sugestão de IA — ver item 25 do pedido) sem
// precisar remodelar a relação.
public class QuestaoItemMatriz
{
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    public int ItemMatrizReferenciaId { get; set; }
    public ItemMatrizReferencia? ItemMatrizReferencia { get; set; }
}
