namespace BancoQuestoes.Models;

// Um "enum" em C# é um tipo que representa um conjunto fixo de valores nomeados.
// Por baixo dos panos, cada valor é um número inteiro (MultiplaEscolha = 0, Discursiva = 1, etc.),
// mas no código você trabalha com o nome, não com o número — isso evita "números mágicos" espalhados.
public enum TipoQuestao
{
    MultiplaEscolha,
    Discursiva,
    CertoErrado,
    Associacao,
    RespostaBreve,
    Numerica,
    Lacunas
}

public enum Dificuldade
{
    Facil,
    Media,
    Dificil
}

// Quem pode ENXERGAR e USAR uma questão (montar prova com ela) — não confundir
// com quem pode EDITAR/EXCLUIR: isso continua sendo só de quem criou (CriadoPorId),
// não importa a visibilidade. Privada é o padrão pra questão nova: o professor
// precisa optar ativamente por compartilhar, em vez de vazar sem querer.
public enum VisibilidadeQuestao
{
    Privada,
    Institucional,
    Compartilhada,
}

// Taxonomia de Bloom (versão revisada) — nível cognitivo que a questão exige
// do aluno, do mais simples (Lembrar) ao mais complexo (Criar). Opcional
// (nullable no Questao): nem toda questão precisa ser classificada, e forçar
// isso em toda questão já existente no banco não faria sentido.
public enum NivelBloom
{
    Lembrar,
    Entender,
    Aplicar,
    Analisar,
    Avaliar,
    Criar,
}

// De onde a questão veio — ajuda a filtrar/entender a procedência do banco.
// "Autoral" é o padrão pra questão nova (o professor escreveu do zero);
// "Importada" é setada automaticamente por QuestaoImportar.razor (Aiken/GIFT).
public enum OrigemQuestao
{
    Autoral,
    Livro,
    Enade,
    Concurso,
    Vestibular,
    Importada,
    Ia,
}

// Natureza/finalidade da prova — puramente informativo (não muda comportamento
// do sistema), mas alimenta o cabeçalho da exportação e futuras estatísticas
// ("quantas P1 essa disciplina já teve", etc.). Opcional: nem toda prova
// precisa ser classificada no momento em que é criada.
public enum TipoProva
{
    P1,
    P2,
    Substitutiva,
    Recuperacao,
    Simulado,
    Lista,
    Atividade,
}

// Como a imagem de uma questão deve ficar posicionada no documento exportado.
// Nullable no QuestaoImagem (não este enum) — null significa "usar o padrão
// do sistema" (Centralizado), evitando precisar de um defaultValue de coluna
// pra um enum-com-conversão (fonte do bug de migração já visto antes aqui).
public enum AlinhamentoImagem
{
    Esquerda,
    Centralizado,
    Direita,
}

// Como a instituição divide o ano letivo — decide se Turma/Prova pedem só
// "Semestre" (1º/2º) ou também "Bimestre" (1º/2º DENTRO de cada semestre,
// ex.: "2026/1º semestre/2º bimestre"). Fica na Instituição (não em Curso ou
// Turma) porque é uma característica institucional, igual o calendário
// acadêmico — todo Curso da mesma Instituição segue o mesmo sistema.
public enum SistemaPeriodos
{
    Semestral,
    SemestralComBimestres,
}

public static class SistemaPeriodosExtensions
{
    public static string Rotulo(this SistemaPeriodos sistema) => sistema switch
    {
        SistemaPeriodos.Semestral => "Semestral (1º/2º semestre)",
        SistemaPeriodos.SemestralComBimestres => "Semestral com bimestres (cada semestre dividido em 1º/2º bimestre)",
        _ => sistema.ToString(),
    };
}

public static class TipoProvaExtensions
{
    // Rótulo em português pra exibição — centralizado aqui porque é usado em
    // vários lugares (ProvaForm, cabeçalho da exportação DOCX/PDF).
    public static string Rotulo(this TipoProva tipo) => tipo switch
    {
        TipoProva.P1 => "P1",
        TipoProva.P2 => "P2",
        TipoProva.Substitutiva => "Substitutiva",
        TipoProva.Recuperacao => "Recuperação",
        TipoProva.Simulado => "Simulado",
        TipoProva.Lista => "Lista",
        TipoProva.Atividade => "Atividade",
        _ => tipo.ToString(),
    };
}

// Rótulos em português pra exibição no histórico de edição (QuestaoHistorico)
// — QuestaoList.razor/QuestaoForm.razor já têm switch-expressions parecidos
// espalhados pra exibição normal (dropdowns, badges); esses aqui existem à
// parte, centralizados, especificamente pra QuestaoService montar as linhas
// "Média → Difícil" do histórico sem depender de código de UI.
public static class DificuldadeExtensions
{
    public static string Rotulo(this Dificuldade dificuldade) => dificuldade switch
    {
        Dificuldade.Facil => "Fácil",
        Dificuldade.Media => "Média",
        Dificuldade.Dificil => "Difícil",
        _ => dificuldade.ToString(),
    };
}

public static class VisibilidadeQuestaoExtensions
{
    public static string Rotulo(this VisibilidadeQuestao visibilidade) => visibilidade switch
    {
        VisibilidadeQuestao.Privada => "Só eu",
        VisibilidadeQuestao.Institucional => "Instituição",
        VisibilidadeQuestao.Compartilhada => "Todos",
        _ => visibilidade.ToString(),
    };
}

public static class NivelBloomExtensions
{
    public static string Rotulo(this NivelBloom bloom) => bloom switch
    {
        NivelBloom.Lembrar => "Lembrar",
        NivelBloom.Entender => "Entender",
        NivelBloom.Aplicar => "Aplicar",
        NivelBloom.Analisar => "Analisar",
        NivelBloom.Avaliar => "Avaliar",
        NivelBloom.Criar => "Criar",
        _ => bloom.ToString(),
    };
}

public static class TipoQuestaoExtensions
{
    // Centraliza o rótulo em português — antes só existia como switch-expression
    // privado duplicado em QuestaoList.razor/QuestaoImportar.razor; ProvaForm.razor
    // (cards de questão disponível, resumo do blueprint) usa esta versão central.
    public static string Rotulo(this TipoQuestao tipo) => tipo switch
    {
        TipoQuestao.MultiplaEscolha => "Múltipla escolha",
        TipoQuestao.Discursiva => "Discursiva",
        TipoQuestao.CertoErrado => "Certo/Errado",
        TipoQuestao.Associacao => "Associação",
        TipoQuestao.RespostaBreve => "Resposta breve",
        TipoQuestao.Numerica => "Numérica",
        TipoQuestao.Lacunas => "Preenchimento de lacunas",
        _ => tipo.ToString(),
    };
}

public static class OrigemQuestaoExtensions
{
    public static string Rotulo(this OrigemQuestao origem) => origem switch
    {
        OrigemQuestao.Autoral => "Autoral",
        OrigemQuestao.Livro => "Livro",
        OrigemQuestao.Enade => "ENADE",
        OrigemQuestao.Concurso => "Concurso",
        OrigemQuestao.Vestibular => "Vestibular",
        OrigemQuestao.Importada => "Importada",
        OrigemQuestao.Ia => "IA",
        _ => origem.ToString(),
    };
}

// Natureza/origem de uma Matriz de Referência (ver MatrizReferencia) — só
// classificação/rotulagem, não muda nenhum comportamento do sistema (mesma
// ideia de OrigemQuestao acima). Deixado propositalmente com "Outro" no
// final pra caber matriz de referência que não se encaixe nos tipos comuns,
// sem precisar de migração pra passar a aceitar um novo tipo específico.
public enum TipoMatrizReferencia
{
    ENADE,
    DCN,
    PPC,
    Institucional,
    Outro,
}

public static class TipoMatrizReferenciaExtensions
{
    public static string Rotulo(this TipoMatrizReferencia tipo) => tipo switch
    {
        TipoMatrizReferencia.ENADE => "ENADE",
        TipoMatrizReferencia.DCN => "DCN",
        TipoMatrizReferencia.PPC => "PPC",
        TipoMatrizReferencia.Institucional => "Institucional",
        TipoMatrizReferencia.Outro => "Outro",
        _ => tipo.ToString(),
    };

    // Escopo duplo (2ª rodada de revisão, ver MatrizReferencia) — único ponto
    // de verdade de "qual Tipo é nacional/AreaCurso vs. institucional/Curso",
    // reusado por MatrizReferenciaService (validação/autorização/listagens) E
    // pelas telas (MatrizForm/MatrizImportarCompleta) pra decidir qual
    // seletor mostrar, sem duplicar essa regra em dois lugares que podem
    // divergir.
    public static bool EhEscopoNacional(this TipoMatrizReferencia tipo) =>
        tipo is TipoMatrizReferencia.ENADE or TipoMatrizReferencia.DCN;
}

// Ciclo de vida de uma Matriz de Referência (item 21/22 do pedido —
// versionamento é obrigatório): Rascunho é uma matriz ainda em montagem
// (não aparece pro professor vincular questões nem no gerador); Ativa é a
// edição vigente daquele Tipo pro curso (aparece por padrão no gerador);
// Historica é uma edição superada por uma mais nova, mas MANTIDA — questões
// já classificadas com ela continuam válidas e visíveis (ver item 21: nunca
// sobrescrever uma matriz antiga só porque surgiu uma edição nova).
public enum StatusMatrizReferencia
{
    Rascunho,
    Ativa,
    Historica,
}

public static class StatusMatrizReferenciaExtensions
{
    public static string Rotulo(this StatusMatrizReferencia status) => status switch
    {
        StatusMatrizReferencia.Rascunho => "Rascunho",
        StatusMatrizReferencia.Ativa => "Ativa",
        StatusMatrizReferencia.Historica => "Histórica",
        _ => status.ToString(),
    };

    // Cor do badge (mesma paleta Bootstrap já usada em Questao.Ativa/
    // qualidade da questão em outras telas do sistema).
    public static string CorBadge(this StatusMatrizReferencia status) => status switch
    {
        StatusMatrizReferencia.Rascunho => "secondary",
        StatusMatrizReferencia.Ativa => "success",
        StatusMatrizReferencia.Historica => "light border text-dark",
        _ => "secondary",
    };
}

// Grupo/natureza de um item dentro de uma Matriz de Referência (ver
// ItemMatrizReferencia) — é o que separa "Perfil do Concluinte" de
// "Competências" e "Conteúdos" na tela, mesmo eles morando na mesma tabela
// (flat, sem hierarquia — ver comentário em ItemMatrizReferencia). Deixado
// com "Outro" no final de propósito (item 3 do pedido: "a enum deve
// permitir expansão futura") — uma matriz que precise de um grupo que não
// se encaixe nos cinco típicos ainda tem onde cair, sem migração.
public enum TipoItemMatriz
{
    PerfilConcluinte,
    Competencia,
    Conteudo,
    Habilidade,
    ObjetoConhecimento,
    Outro,
}

public static class TipoItemMatrizExtensions
{
    public static string Rotulo(this TipoItemMatriz tipo) => tipo switch
    {
        TipoItemMatriz.PerfilConcluinte => "Perfil do Concluinte",
        TipoItemMatriz.Competencia => "Competência",
        TipoItemMatriz.Conteudo => "Conteúdo",
        TipoItemMatriz.Habilidade => "Habilidade",
        TipoItemMatriz.ObjetoConhecimento => "Objeto de Conhecimento",
        TipoItemMatriz.Outro => "Outro",
        _ => tipo.ToString(),
    };

    // Rótulo no plural — usado como título de cada grupo nas telas de
    // gestão/vínculo ("COMPETÊNCIAS", "CONTEÚDOS"...), ver item 8 do pedido.
    public static string RotuloPlural(this TipoItemMatriz tipo) => tipo switch
    {
        TipoItemMatriz.PerfilConcluinte => "Perfil do Concluinte",
        TipoItemMatriz.Competencia => "Competências",
        TipoItemMatriz.Conteudo => "Conteúdos",
        TipoItemMatriz.Habilidade => "Habilidades",
        TipoItemMatriz.ObjetoConhecimento => "Objetos de Conhecimento",
        TipoItemMatriz.Outro => "Outros",
        _ => tipo.ToString(),
    };
}
