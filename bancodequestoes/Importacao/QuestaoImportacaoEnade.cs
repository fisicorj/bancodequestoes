using BancoQuestoes.Models;

namespace BancoQuestoes.Importacao;

// Modelo intermediário do importador ENADE: objeto achatado em memória, nunca uma
// Questao de verdade até a confirmação (ImportadorProvaEnadeService.ImportarSelecionadasAsync).
public enum StatusPreImportacaoEnade
{
    // Estado inicial, logo após a extração do PDF, antes de qualquer classificação rodar.
    Extraida,

    // Extraída com sucesso mas com pelo menos um Alerta; ainda pode ser importada
    // se Selecionada continuar marcada (RequerRevisao não bloqueia, só sinaliza).
    RequerRevisao,

    // Extraída sem nenhum alerta pendente — pronta pra importar como está.
    Validada,

    // Não deu pra extrair o mínimo necessário — Selecionada nasce falsa.
    ComErro,

    // Professor decidiu explicitamente não importar — decisão ativa, diferente
    // de ComErro, pra o resumo distinguir "por decisão" de "por erro".
    Ignorada,

    // Setado pelo Service, por item, depois de ImportarSelecionadasAsync criar a Questao.
    Importada,
}

public class AlternativaImportacaoEnade
{
    public char Letra { get; set; }
    public string Texto { get; set; } = "";
}

// Imagem em memória, mesmo papel de PendenteImagem mas com origem no parser: é
// conteúdo pedagógico, nunca confundir com RepresentacaoOriginalEnade (só auditoria).
public class ImagemImportacaoEnade
{
    public required byte[] Conteudo { get; set; }
    public required string ContentType { get; set; }
    public string NomeArquivo { get; set; } = "";

    // Página do PDF de onde a imagem foi extraída — só informativo.
    public int PaginaOrigem { get; set; }
}

// Referência visual da página original do PDF, só pra conferência/auditoria — nunca
// vira QuestaoImagem. Conteudo é nullable: se a renderização falhar, sobra só a página.
public class RepresentacaoOriginalEnade
{
    public int Pagina { get; set; }
    public byte[]? Conteudo { get; set; }
    public string ContentType { get; set; } = "image/png";
}

// Um subitem (a, b, c...) de uma questão discursiva, só quando o parser do padrão de
// resposta consegue separar com segurança; Pontuacao/RespostaPadrao vêm do documento oficial.
public class ItemDiscursivoImportacaoEnade
{
    public string Codigo { get; set; } = "";
    public string? Enunciado { get; set; }
    public decimal? Pontuacao { get; set; }
    public string RespostaPadrao { get; set; } = "";
}

// Uma questão candidata extraída do PDF da prova — campos alinhados com
// QuestaoInput/Questao pra a conversão final ser um mapeamento direto, sem regra nova.
public class QuestaoImportacaoEnade
{
    // Id sequencial só dentro deste lote, pra UI endereçar "editar a questão N"
    // sem Id de banco. Nunca confundir com Questao.Id.
    public int IdLote { get; set; }

    public int? PaginaOrigem { get; set; }

    // Última página do PDF da prova que pertence a esta questão. Junto com
    // PaginaOrigem delimita o intervalo usado pra achar imagens e gerar snapshots.
    public int? PaginaFim { get; set; }

    // Igual Questao.NumeroOriginal — string (aceita "01", "D1"...). Vazio = não
    // identificado pelo parser (vira Alerta e Status=RequerRevisao/ComErro).
    public string NumeroOriginal { get; set; } = "";

    public SecaoEnade? Secao { get; set; }

    public TipoQuestao Tipo { get; set; } = TipoQuestao.MultiplaEscolha;

    public string Enunciado { get; set; } = "";

    // Múltipla escolha: o parser só reconhece Objetiva (A-E) e Discursiva.
    public List<AlternativaImportacaoEnade> Alternativas { get; set; } = new();

    // Gabarito: sempre do arquivo oficial ou preenchido à mão na revisão, nunca
    // inferido do enunciado. Índice em Alternativas; null = gabarito pendente.
    public int? RespostaCorretaIndex { get; set; }

    // Discursiva: o parser nunca inventa a resposta. Vazio após extrair só a prova;
    // com PDF de Padrão de Resposta, o service preenche com o texto oficial completo.
    public string RespostaEsperadaDiscursiva { get; set; } = "";

    // Rubrica/critério de correção, mapeia pra QuestaoDiscursiva.CriterioAvaliacao;
    // preenchido, quando possível, com uma reorganização legível dos ItensDiscursivos.
    public string? CriterioAvaliacaoDiscursiva { get; set; }

    // Subitens com pontuação, quando o padrão de resposta permite separá-los com
    // segurança (ver ItemDiscursivoImportacaoEnade); lista vazia é o caso comum.
    public List<ItemDiscursivoImportacaoEnade> ItensDiscursivos { get; set; } = new();

    // Página(s) do PDF do padrão de resposta de onde foi lido — só informativo,
    // nulo quando não há padrão associado.
    public int? PaginaOrigemPadraoResposta { get; set; }

    public List<ImagemImportacaoEnade> Imagens { get; set; } = new();

    // Referências visuais das páginas originais da prova onde a questão apareceu
    // (pode abranger mais de uma). Nunca confundir com Imagens nem persistir como QuestaoImagem.
    public List<RepresentacaoOriginalEnade> RepresentacoesOriginais { get; set; } = new();

    // Mesma ideia de RepresentacoesOriginais, mas da página do Padrão de Resposta;
    // só preenchida pra Discursivas com PaginaOrigemPadraoResposta definido.
    public List<RepresentacaoOriginalEnade> RepresentacoesOriginaisPadraoResposta { get; set; } = new();

    // Contexto da importação inteira, repetido aqui só pra converter em
    // QuestaoInput sem voltar no objeto pai.
    public int Ano { get; set; }

    // Só Componente Específico usa (Formação Geral nunca tem Área). Pré-preenchido
    // com a Área da tela inicial; nulo pra Formação Geral.
    public int? AreaCursoId { get; set; }

    // Formação Geral: pré-preenchido automaticamente com Disciplina/Assunto
    // "Formação Geral". Componente Específico: nasce vazio (0), professor escolhe na revisão.
    public int DisciplinaId { get; set; }
    public int AssuntoId { get; set; }

    // Bloom/Dificuldade opcionais na revisão, nunca classificados automaticamente;
    // Dificuldade fica em Media (não é nullable) até o professor mudar.
    public Dificuldade Dificuldade { get; set; } = Dificuldade.Media;
    public NivelBloom? Bloom { get; set; }

    public StatusPreImportacaoEnade Status { get; set; } = StatusPreImportacaoEnade.Extraida;

    // Nunca "corrige" nada em silêncio — todo problema detectado vira uma string
    // aqui, mostrada na revisão (motivos documentados em EnadeProvaParser).
    public List<string> Alertas { get; set; } = new();

    // Resultado do cruzamento contra o banco: sinaliza "possível duplicata" sem bloquear.
    public bool PossivelDuplicata { get; set; }
    public int? QuestaoDuplicadaId { get; set; }

    // Itens de Alinhamento Curricular/ENADE que esta questão avalia — nunca
    // inferido pelo parser (que é 100% determinístico); só populado na
    // revisão, manualmente ou por sugestão de IA sempre confirmada pelo
    // professor (ver RevisaoQuestaoEnade.razor). Mapeado 1:1 pra
    // QuestaoInput.ItemMatrizIds em ImportadorProvaEnadeService.
    public HashSet<int> ItemMatrizIds { get; set; } = new();

    // Marcado quando o professor clica "Salvar revisão" nesta questão pelo menos
    // uma vez (RevisaoQuestaoEnade.razor) — só rastreia progresso na tela de
    // revisão do lote, nunca é persistido; não substitui Status (que continua
    // refletindo Alertas de verdade, ver RevisaoEnadeHelper.Revalidar).
    public bool RevisaoSalva { get; set; }

    // Marcada por padrão, exceto quando Status nasce ComErro, pra não convidar
    // o professor a importar algo visivelmente quebrado.
    public bool Selecionada { get; set; } = true;
}

// Resultado completo de um processamento de prova ENADE — Ano/AreaCursoId são o
// contexto escolhido na tela inicial, válidos pro lote inteiro.
public class ResultadoImportacaoEnade
{
    public int Ano { get; set; }
    public int AreaCursoId { get; set; }
    public string? NomeAreaCurso { get; set; }

    public List<QuestaoImportacaoEnade> Questoes { get; set; } = new();

    // Problemas que não são de uma questão específica (ex.: gabarito com N
    // questões não encontradas no PDF da prova).
    public List<string> AlertasGerais { get; set; } = new();

    // Quais dos três documentos foram informados; usado só pelo resumo da tela
    // de revisão (a Prova é sempre obrigatória, então sempre true).
    public bool TinhaGabarito { get; set; }
    public bool TinhaPadraoResposta { get; set; }
}

// Resultado da importação definitiva: distingue claramente importadas, ignoradas
// e com erro. Preenchido por ImportadorProvaEnadeService.ImportarSelecionadasAsync.
public class ResultadoImportacaoDefinitivaEnade
{
    public List<QuestaoImportacaoEnade> Importadas { get; set; } = new();
    public List<QuestaoImportacaoEnade> Ignoradas { get; set; } = new();
    public List<(QuestaoImportacaoEnade Questao, string Erro)> ComErro { get; set; } = new();
}
