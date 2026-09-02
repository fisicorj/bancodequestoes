using System.ComponentModel.DataAnnotations;
using BancoQuestoes.Models;

namespace BancoQuestoes.Services;

// Movido de InstituicaoForm.razor (classe privada) — mesmo padrão dos
// outros Inputs.
public sealed class InstituicaoInput
{
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Telefone { get; set; }
    public string? Site { get; set; }
    public string? Instrucoes { get; set; }

    // Anulável de propósito, mesmo a Instituicao (entidade) sendo
    // não-anulável: é isso que obriga o formulário a começar sem nada
    // pré-selecionado — o professor precisa escolher ativamente, em vez de
    // aceitar sem querer um valor padrão. Ver InstituicaoService pra
    // validação (o DataAnnotationsValidator já bloqueia o submit, mas o
    // Service confere de novo — mesmo padrão de defesa em profundidade do
    // resto do sistema).
    [Required(ErrorMessage = "Escolha o sistema de períodos.")]
    public SistemaPeriodos? SistemaPeriodos { get; set; }
}

// Logo recém-enviada pelo <InputFile>, ainda não salva — a tela mantém isso
// à parte do modelo pra poder mostrar a prévia e permitir cancelar antes de
// confirmar o Salvar.
public sealed class LogoPendente
{
    public required string ContentType { get; set; }
    public required byte[] Conteudo { get; set; }
}
