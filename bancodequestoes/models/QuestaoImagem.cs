namespace BancoQuestoes.Models;

public class QuestaoImagem
{
    public int Id { get; set; }

    // Aqui a FK aponta para a classe base Questao, não para um subtipo específico —
    // qualquer tipo de questão pode ter imagens.
    public int QuestaoId { get; set; }
    public Questao? Questao { get; set; }

    // Guardamos o arquivo direto no Postgres (coluna "bytea"), em vez de um Path
    // para um arquivo em disco. Mais simples de operar num projeto pequeno (um
    // pg_dump já leva as imagens junto, não precisa sincronizar disco + banco),
    // com a contrapartida de deixar a tabela mais pesada — aceitável na escala
    // deste sistema.
    public required byte[] Conteudo { get; set; }
    public required string ContentType { get; set; }
    public required string NomeArquivo { get; set; }

    public int Ordem { get; set; }

    // Legenda: texto exibido logo abaixo da imagem no DOCX/PDF exportado
    // (ex.: "Figura 1 — Circuito analisado na questão"). Opcional.
    public string? Legenda { get; set; }

    // Texto alternativo: só usado no HTML da prévia/tela (atributo alt da
    // <img>) — formatos impressos (DOCX/PDF) não têm equivalente visual,
    // então esse campo não aparece no documento exportado.
    public string? TextoAlternativo { get; set; }

    // Nulo = usa o padrão do sistema (ver AlinhamentoImagem). Todo o resto
    // desses campos de imagem segue o mesmo raciocínio: nullable pra não
    // precisar de defaultValue de coluna.
    public AlinhamentoImagem? Alinhamento { get; set; }

    // Percentual da largura máxima de imagem da página (ver LarguraMaximaImagemCm
    // nos exportadores). Nulo = usa o padrão do sistema.
    public int? LarguraPercentual { get; set; }
}
