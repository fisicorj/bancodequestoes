namespace BancoQuestoes.Models;

// Dados de cabeçalho reutilizados na exportação da prova (DOCX/PDF): nome,
// identidade visual (logo) e informações de contato/endereço. Uma Prova pode
// referenciar uma Instituicao para puxar esse cabeçalho automaticamente.
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

    // Como essa instituição divide o ano letivo — ver o enum SistemaPeriodos.
    // Não-anulável de propósito (toda instituição PRECISA ter um valor
    // concreto pra Turma/Prova saberem se pedem Bimestre ou não); quem
    // garante que o professor escolheu isso conscientemente ao CADASTRAR uma
    // instituição nova é a validação em InstituicaoService (o formulário não
    // vem com nada pré-selecionado). Instituições que já existiam antes dessa
    // coluna existir foram migradas pra Semestral (ver a migração) — editar
    // uma delas mostra esse valor pré-selecionado, não força escolher de novo.
    public SistemaPeriodos SistemaPeriodos { get; set; }

    // Logo guardado no Postgres, mesmo esquema usado em QuestaoImagem.
    public byte[]? LogoConteudo { get; set; }
    public string? LogoContentType { get; set; }
}
