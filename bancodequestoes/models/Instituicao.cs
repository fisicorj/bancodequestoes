namespace BancoQuestoes.Models;

// Dados de cabeçalho reutilizados na exportação da prova (DOCX/PDF): nome, logo e
// contato/endereço. Uma Prova pode referenciar uma Instituicao pra puxar isso automaticamente.
public class Instituicao
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Telefone { get; set; }
    public string? Site { get; set; }

    // Instruções padrão da prova (uma por linha), tipo "Não será aceita revisão
    // feita a lápis". Reaproveitadas em toda prova exportada dessa instituição.
    public string? Instrucoes { get; set; }

    // Como a instituição divide o ano letivo. Não-anulável: InstituicaoService valida a
    // escolha no cadastro; instituições pré-existentes migraram pra Semestral.
    public SistemaPeriodos SistemaPeriodos { get; set; }

    // Logo guardado no Postgres, mesmo esquema usado em QuestaoImagem.
    public byte[]? LogoConteudo { get; set; }
    public string? LogoContentType { get; set; }
}
