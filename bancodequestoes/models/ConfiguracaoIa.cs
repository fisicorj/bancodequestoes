using System.ComponentModel.DataAnnotations.Schema;

namespace BancoQuestoes.Models;

// Local == Ollama na própria máquina (nada sai da rede); Nuvem == Ollama Cloud, mesma
// API nativa mas autenticada com API key, pra quem não tem hardware pra rodar localmente.
public enum ModoExecucaoIa
{
    Local,
    Nuvem,
}

// Configuração ÚNICA e GLOBAL (Id = 1), editável via /admin/configuracao-ia sem
// reiniciar o app; appsettings.json "Ollama" vira só o valor inicial dessa tela.
public class ConfiguracaoIa
{
    public int Id { get; set; }

    public bool Habilitada { get; set; }

    public ModoExecucaoIa Modo { get; set; } = ModoExecucaoIa.Local;

    // --- Modo Local ---
    public string BaseUrlLocal { get; set; } = "http://localhost:11434";

    public string ModeloLocal { get; set; } = "qwen3:8b";

    // Modo Nuvem: nullable porque só é obrigatória quando Modo == Nuvem
    // (checado em ConfiguracaoIaService.AtualizarAsync).
    public string? ApiKeyNuvem { get; set; }

    public string ModeloNuvem { get; set; } = "gpt-oss:120b";

    // --- Modelo de visão (opcional) ---
    // Separado do modelo "de texto" acima porque nem todo modelo aceita imagem
    // (ex.: qwen3:8b não suporta; precisa de algo como qwen2.5vl, gemma3, llava).
    // Null/vazio = a retranscrição visual do Importador ENADE fica indisponível,
    // mesmo com Habilitada=true — nunca cai pro modelo de texto por engano.
    public string? ModeloVisaoLocal { get; set; }

    public string? ModeloVisaoNuvem { get; set; }

    public int TimeoutSegundos { get; set; } = 90;

    // Separado de TimeoutSegundos: inferência com imagem é bem mais lenta que
    // texto puro (encoder visual + geração), e a primeira chamada ainda paga o
    // custo de carregar o modelo de visão na memória — 90s costuma não bastar.
    // Só usado pela retranscrição visual; as chamadas de texto nunca leem isto.
    public int TimeoutVisaoSegundos { get; set; } = 300;

    // URL/modelo efetivos conforme o Modo — SugestaoIaService usa só isso, nunca lê
    // BaseUrlLocal/ModeloLocal/ModeloNuvem direto.
    [NotMapped]
    public string BaseUrlEfetiva => Modo == ModoExecucaoIa.Nuvem ? "https://ollama.com" : BaseUrlLocal;

    [NotMapped]
    public string ModeloEfetivo => Modo == ModoExecucaoIa.Nuvem ? ModeloNuvem : ModeloLocal;

    [NotMapped]
    public string? ModeloVisaoEfetivo => Modo == ModoExecucaoIa.Nuvem ? ModeloVisaoNuvem : ModeloVisaoLocal;
}
