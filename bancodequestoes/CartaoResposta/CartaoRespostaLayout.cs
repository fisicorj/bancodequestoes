namespace BancoQuestoes.CartaoResposta;

// Geometria do cartão resposta impresso — ÚNICA fonte de verdade das coordenadas, em pontos
// PDF (1/72 polegada), compartilhada entre CartaoRespostaPdfExporter (desenha o cartão) e
// LeitorCartaoRespostaService (interpreta a foto do cartão preenchido). Os dois PRECISAM
// concordar exatamente nessas posições — se um mudar sem o outro, a leitura desalinha e passa
// a marcar a bolha errada silenciosamente.
public static class CartaoRespostaLayout
{
    // A4 em pontos (210x297mm), mesmo tamanho usado no resto da exportação do sistema.
    public const double LarguraPagina = 595.28;
    public const double AlturaPagina = 841.89;

    public const double Margem = 40;

    // 4 marcadores pretos sólidos, um em CADA canto — usados pra calcular a correção de
    // perspectiva da foto (homografia a partir de 4 pontos). Deliberadamente NÃO usamos o QR
    // Code como um dos 4 pontos: o centro geométrico de um QR não coincide exatamente com o
    // centroide dos "finder patterns" que o decodificador devolve, o que introduziria um erro
    // sistemático na perspectiva — 4 marcadores idênticos, todos localizados do mesmo jeito
    // (centroide de pixel escuro), evitam essa assimetria. Tamanho generoso (18pt ≈ 6mm) pra
    // ficar robusto a foto de celular com alguma distância/ângulo.
    public const double TamanhoMarcador = 18;

    // QR Code fica centralizado no topo — não é um dos pontos da homografia, só serve pra
    // identificar o aluno (a leitura do QR em si usa o próprio decodificador ZXing, que já
    // tolera alguma perspectiva sozinho). DELIBERADAMENTE centralizado (não perto de nenhum
    // canto): LeitorCartaoRespostaService procura cada marcador dentro de um quadrante lateral
    // (ex.: canto superior direito = x em [0.65,1], y em [0,0.35] da foto) — se o QR estivesse
    // perto de um canto, seus módulos escuros cairiam dentro dessa janela de busca e puxariam
    // o centroide do marcador pra longe da posição real, corrompendo a homografia.
    public const double TamanhoQr = 80;

    // Grade de bolhas: uma linha por questão, uma coluna por alternativa (A até MaxAlternativas
    // de CartaoRespostaService). Espaçamento generoso pra bolha ficar preenchível a caneta sem
    // encostar na vizinha. Começa bem mais abaixo (260) pra sobrar espaço suficiente pro QR
    // centralizado + cabeçalho (aluno/turma/disciplina/tipo) + instrução + rótulos A/B/C/D das
    // colunas, sem nenhum desses textos encavalar — eram só 230 antes e a instrução colidia
    // com os rótulos de coluna (bug reportado com captura de tela).
    public const double TopoGradeBolhas = 260;
    public const double AlturaLinhaBolha = 26;
    public const double RaioBolha = 7;
    public const double EspacamentoColunaBolha = 32;
    public const double InicioColunasBolha = Margem + 70;

    public static (double X, double Y) MarcadorTopoEsquerdo() => (Margem, Margem);

    public static (double X, double Y) MarcadorTopoDireito() => (LarguraPagina - Margem - TamanhoMarcador, Margem);

    public static (double X, double Y) MarcadorBaseEsquerdo() => (Margem, AlturaPagina - Margem - TamanhoMarcador);

    public static (double X, double Y) MarcadorBaseDireito() => (LarguraPagina - Margem - TamanhoMarcador, AlturaPagina - Margem - TamanhoMarcador);

    // Centralizado horizontalmente no topo, entre os dois marcadores superiores (ver
    // explicação acima do porquê NÃO fica perto de nenhum canto).
    public static (double X, double Y) QrCabecalho() =>
        ((LarguraPagina - TamanhoQr) / 2, Margem);

    // Y da linha da questão de índice `indiceQuestao` (0-based) — mesma linha vale pro
    // número da questão e pra todas as bolhas dela.
    public static double LinhaY(int indiceQuestao) => TopoGradeBolhas + indiceQuestao * AlturaLinhaBolha;

    // Centro da bolha da alternativa `indiceAlternativa` (0 = A, 1 = B...) na linha da
    // questão de índice `indiceQuestao` (0-based, na ordem em que aparecem no cartão).
    public static (double X, double Y) CentroBolha(int indiceQuestao, int indiceAlternativa) =>
        (InicioColunasBolha + indiceAlternativa * EspacamentoColunaBolha, LinhaY(indiceQuestao));

    // Quantas questões cabem antes de estourar a página — CartaoRespostaPdfExporter usa isso
    // pra decidir quando quebrar linha/página (provas muito longas ficam em mais de uma
    // página de bolhas, repetindo QR/marcadores em cada uma).
    public static int QuestoesPorPagina() =>
        (int)((AlturaPagina - Margem - TopoGradeBolhas) / AlturaLinhaBolha);
}
