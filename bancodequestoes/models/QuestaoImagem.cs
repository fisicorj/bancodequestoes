namespace BancoQuestoes.Models;

public class QuestaoImagem
{
    public int Id { get; set; }

    // Aqui a FK aponta para a classe base Questao, não para um subtipo específico —
    // qualquer tipo de questão pode ter imagens.
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    // Arquivo direto no Postgres (bytea), não um Path em disco: um pg_dump já leva as
    // imagens junto; tabela mais pesada é aceitável na escala deste sistema.
    public required byte[] Conteudo { get; set; }
    public required string ContentType { get; set; }
    public required string NomeArquivo { get; set; }

    public int Ordem { get; set; }

    // Legenda: texto exibido logo abaixo da imagem no DOCX/PDF exportado
    // (ex.: "Figura 1 — Circuito analisado na questão"). Opcional.
    public string? Legenda { get; set; }

    // Só usado no HTML da prévia (atributo alt) — não aparece no DOCX/PDF exportado.
    public string? TextoAlternativo { get; set; }

    // Nulo = usa o padrão do sistema; campos de imagem são nullable pra não
    // precisar de defaultValue de coluna.
    public AlinhamentoImagem? Alinhamento { get; set; }

    // Percentual da largura máxima de imagem da página (ver LarguraMaximaImagemCm
    // nos exportadores). Nulo = usa o padrão do sistema.
    public int? LarguraPercentual { get; set; }
}
