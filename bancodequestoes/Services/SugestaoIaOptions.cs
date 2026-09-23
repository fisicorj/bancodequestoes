namespace BancoQuestoes.Services;

// Configuração da integração com Ollama local (seção "Ollama" do
// appsettings.json); tudo tem valor padrão pra funcionar out-of-the-box.
public class SugestaoIaOptions
{
    public bool Habilitada { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:11434";

    // Qwen3 8B: lida melhor com saída JSON estruturada e roda bem em CPU com 16GB de RAM.
    public string Modelo { get; set; } = "qwen3:8b";

    // 30s era curto demais: a primeira chamada, com o modelo ainda carregando, facilmente passa disso.
    public int TimeoutSegundos { get; set; } = 90;
}
