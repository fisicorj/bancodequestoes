namespace BancoQuestoes.Models;

// Matriz curricular (ENADE, DCN, PPC etc.), versionada (edições antigas coexistem). Escopo
// duplo: exatamente um entre CursoId e AreaCursoId (ENADE/DCN) é preenchido.
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

    // Fonte documental — nenhum é obrigatório porque nem toda matriz tem
    // portaria/URL publicada (ex.: uma matriz institucional informal).
    public string? Orgao { get; set; }
    public string? Documento { get; set; }
    public string? UrlFonte { get; set; }

    public string? Descricao { get; set; }

    public StatusMatrizReferencia Status { get; set; } = StatusMatrizReferencia.Rascunho;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<ItemMatrizReferencia> Itens { get; set; } = new();

    // "Matriz Rascunho não pode receber vínculo" (Ativa/Historica são OK). Só pra
    // checagens em memória — EF Core não traduz isso em .Where().
    public bool PodeSerUtilizadaEmNovoVinculo() => Status != StatusMatrizReferencia.Rascunho;
}

// Item de uma matriz (competência, item do perfil, conteúdo, habilidade). Sem
// auto-relacionamento: ENADE/DCN mostram grupos FLAT, Tipo já agrupa nas telas.
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

    // Desativar em vez de apagar preserva o histórico de questões já vinculadas
    // (mesmo padrão de Questao.Ativa).
    public bool Ativo { get; set; } = true;
}

// Vínculo muitos-pra-muitos entre Questao e ItemMatrizReferencia. Classe própria (não
// implícita) deixa espaço pra ganhar colunas no futuro (peso, origem de sugestão de IA).
public class QuestaoItemMatriz
{
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    public int ItemMatrizReferenciaId { get; set; }
    public ItemMatrizReferencia? ItemMatrizReferencia { get; set; }
}
