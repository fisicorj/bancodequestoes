namespace BancoQuestoes.Models;

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

// Quem pode ENXERGAR/USAR a questão — não quem pode editar (sempre CriadoPorId).
// Privada é o padrão: o professor opta ativamente por compartilhar.
public enum VisibilidadeQuestao
{
    Privada,
    Institucional,
    Compartilhada,
}

// Taxonomia de Bloom: nível cognitivo do mais simples (Lembrar) ao mais complexo
// (Criar). Nullable no Questao: nem toda questão precisa ser classificada.
public enum NivelBloom
{
    Lembrar,
    Entender,
    Aplicar,
    Analisar,
    Avaliar,
    Criar,
}

// "Autoral" é o padrão pra questão nova; "Importada" é setada automaticamente
// por QuestaoImportar.razor (Aiken/GIFT).
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

// Puramente informativo (não muda comportamento) — alimenta o cabeçalho da
// exportação e futuras estatísticas.
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

// Diferente de TipoProva (só rótulo): muda de verdade o POOL de questões elegíveis e as
// regras de distribuição (ver GeradorProvaService). Curso = todas as Disciplinas, usado pro "Simulado ENADE".
public enum TipoEscopoProva
{
    Disciplina = 1,
    Multidisciplinar = 2,
    Curso = 3,
}

// Nullable no QuestaoImagem: null = usar o padrão do sistema (Centralizado),
// evitando defaultValue de coluna pra um enum-com-conversão.
public enum AlinhamentoImagem
{
    Esquerda,
    Centralizado,
    Direita,
}

// Decide se Turma/Prova pedem só "Semestre" ou também "Bimestre" dentro dele. Fica
// na Instituição porque todo Curso dela segue o mesmo calendário.
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
    // Centralizado aqui porque é usado em vários lugares (ProvaForm, exportação).
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

// Existe à parte (não reusa os switches de QuestaoList/QuestaoForm) pra QuestaoService
// montar linhas "Média → Difícil" do histórico sem depender de código de UI.
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
    // Antes duplicado como switch privado em QuestaoList/QuestaoImportar; agora
    // centralizado, ProvaForm também usa esta versão.
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

// Só relevante quando Questao.Origem == Enade: "Formação Geral" (D1, comum a todos os
// cursos) vs "Componente Específico" (D2, núcleo técnico da Área) — recorte ortogonal a AreaCurso/OrigemQuestao.
public enum SecaoEnade
{
    FormacaoGeral,
    ComponenteEspecifico,
}

public static class SecaoEnadeExtensions
{
    public static string Rotulo(this SecaoEnade secao) => secao switch
    {
        SecaoEnade.FormacaoGeral => "Formação Geral",
        SecaoEnade.ComponenteEspecifico => "Componente Específico",
        _ => secao.ToString(),
    };
}

// Só classificação/rotulagem, não muda comportamento. "Outro" no final cobre matriz
// que não se encaixe nos tipos comuns, sem precisar de migração.
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

    // Único ponto de verdade de "qual Tipo é nacional/AreaCurso vs. institucional/Curso",
    // reusado por MatrizReferenciaService e pelas telas (MatrizForm etc).
    public static bool EhEscopoNacional(this TipoMatrizReferencia tipo) =>
        tipo is TipoMatrizReferencia.ENADE or TipoMatrizReferencia.DCN;
}

// Rascunho: matriz em montagem, não aparece pro professor vincular. Ativa: edição
// vigente. Historica: edição superada, mas MANTIDA — questões classificadas continuam válidas.
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

    // Mesma paleta Bootstrap já usada em Questao.Ativa/qualidade da questão.
    public static string CorBadge(this StatusMatrizReferencia status) => status switch
    {
        StatusMatrizReferencia.Rascunho => "secondary",
        StatusMatrizReferencia.Ativa => "success",
        StatusMatrizReferencia.Historica => "light border text-dark",
        _ => "secondary",
    };
}

// Separa "Perfil do Concluinte" de "Competências"/"Conteúdos" na tela, mesmo morando na
// mesma tabela flat. "Outro" no final cobre grupos que não se encaixem, sem migração.
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

    // Usado como título de cada grupo nas telas de gestão/vínculo.
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

// Aberta aceita novas tentativas; Encerrada (manual, pelo professor) bloqueia novos acessos
// mesmo dentro do prazo — diferente de DataLimite vencida, que é checada à parte.
public enum StatusAplicacaoProva
{
    Aberta,
    Encerrada,
}

public static class StatusAplicacaoProvaExtensions
{
    public static string Rotulo(this StatusAplicacaoProva status) => status switch
    {
        StatusAplicacaoProva.Aberta => "Aberta",
        StatusAplicacaoProva.Encerrada => "Encerrada",
        _ => status.ToString(),
    };
}

// EmAndamento: aluno ainda respondendo. Enviada: terminou (normal ou por encerramento
// forçado), já corrigida automaticamente, mas a nota fica escondida do aluno até o professor
// revisar. Liberada: professor liberou a nota pro aluno ver.
public enum StatusRespostaProvaOnline
{
    EmAndamento,
    Enviada,
    Liberada,
}

public static class StatusRespostaProvaOnlineExtensions
{
    public static string Rotulo(this StatusRespostaProvaOnline status) => status switch
    {
        StatusRespostaProvaOnline.EmAndamento => "Em andamento",
        StatusRespostaProvaOnline.Enviada => "Aguardando liberação",
        StatusRespostaProvaOnline.Liberada => "Liberada",
        _ => status.ToString(),
    };
}

// Por que a tentativa terminou. Nulo enquanto EmAndamento; EnviadoPeloAluno é o caminho
// normal (botão Enviar), os demais são o encerramento forçado do anti-cola — detecção via JS
// de blur/visibilitychange/fullscreenchange, nunca 100% à prova de burla, só resposta ao que
// o navegador consegue detectar.
public enum MotivoEncerramento
{
    EnviadoPeloAluno,
    PerdaDeFoco,
    SaidaDeTelaCheia,
}

public static class MotivoEncerramentoExtensions
{
    public static string Rotulo(this MotivoEncerramento motivo) => motivo switch
    {
        MotivoEncerramento.EnviadoPeloAluno => "Enviado pelo aluno",
        MotivoEncerramento.PerdaDeFoco => "Encerrado — saiu da tela/trocou de janela",
        MotivoEncerramento.SaidaDeTelaCheia => "Encerrado — saiu da tela cheia",
        _ => motivo.ToString(),
    };
}
