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

    // Anulável de propósito: obriga o formulário a começar sem nada
    // pré-selecionado, pro professor escolher ativamente.
    [Required(ErrorMessage = "Escolha o sistema de períodos.")]
    public SistemaPeriodos? SistemaPeriodos { get; set; }
}

// Logo recém-enviada, ainda não salva — separada do modelo pra permitir prévia/cancelar antes do Salvar.
public sealed class LogoPendente
{
    public required string ContentType { get; set; }
    public required byte[] Conteudo { get; set; }
}
