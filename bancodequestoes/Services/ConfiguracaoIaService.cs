using BancoQuestoes.Data;
using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BancoQuestoes.Services;

// CRUD da configuração ÚNICA e GLOBAL de IA (sempre linha Id=1),
// editável via /admin/configuracao-ia. Só Admin altera — afeta o sistema todo.
public class ConfiguracaoIaService(ApplicationDbContext db, IOptions<SugestaoIaOptions> defaultsAntigos)
{
    private const int IdUnico = 1;

    // Cria a linha sob demanda, semeada com os valores antigos de
    // appsettings.json "Ollama" — depois disso, só o banco manda.
    public async Task<ConfiguracaoIa> ObterAsync()
    {
        var config = await db.ConfiguracoesIa.FindAsync(IdUnico);
        if (config is not null)
        {
            return config;
        }

        var antigas = defaultsAntigos.Value;
        config = new ConfiguracaoIa
        {
            Id = IdUnico,
            Habilitada = antigas.Habilitada,
            Modo = ModoExecucaoIa.Local,
            BaseUrlLocal = string.IsNullOrWhiteSpace(antigas.BaseUrl) ? "http://localhost:11434" : antigas.BaseUrl,
            ModeloLocal = string.IsNullOrWhiteSpace(antigas.Modelo) ? "qwen3:8b" : antigas.Modelo,
            TimeoutSegundos = antigas.TimeoutSegundos > 0 ? antigas.TimeoutSegundos : 90,
        };

        db.ConfiguracoesIa.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    public async Task AtualizarAsync(ConfiguracaoIa modelo, bool ehAdmin)
    {
        if (!ehAdmin)
        {
            throw new OperacaoInvalidaException(
                "Apenas administradores podem alterar a configuração de IA — ela vale pro sistema inteiro, não só pra sua conta.");
        }

        if (modelo.Modo == ModoExecucaoIa.Local && string.IsNullOrWhiteSpace(modelo.BaseUrlLocal))
        {
            throw new OperacaoInvalidaException("Informe a URL do Ollama local (ex.: http://localhost:11434).");
        }

        if (modelo.Modo == ModoExecucaoIa.Nuvem && string.IsNullOrWhiteSpace(modelo.ApiKeyNuvem))
        {
            throw new OperacaoInvalidaException("Informe a API key da Ollama Cloud (gerada em ollama.com/settings/keys).");
        }

        if (string.IsNullOrWhiteSpace(modelo.Modo == ModoExecucaoIa.Nuvem ? modelo.ModeloNuvem : modelo.ModeloLocal))
        {
            throw new OperacaoInvalidaException("Informe o nome do modelo.");
        }

        if (modelo.TimeoutSegundos is < 5 or > 600)
        {
            throw new OperacaoInvalidaException("O timeout deve ficar entre 5 e 600 segundos.");
        }

        // Teto maior que o timeout normal: inferência com imagem é bem mais lenta
        // (e a primeira chamada ainda carrega o modelo de visão na memória).
        if (modelo.TimeoutVisaoSegundos is < 5 or > 900)
        {
            throw new OperacaoInvalidaException("O timeout da retranscrição visual deve ficar entre 5 e 900 segundos.");
        }

        var config = await ObterAsync();
        config.Habilitada = modelo.Habilitada;
        config.Modo = modelo.Modo;
        config.BaseUrlLocal = modelo.BaseUrlLocal.Trim();
        config.ModeloLocal = modelo.ModeloLocal.Trim();
        config.ApiKeyNuvem = string.IsNullOrWhiteSpace(modelo.ApiKeyNuvem) ? null : modelo.ApiKeyNuvem.Trim();
        config.ModeloNuvem = modelo.ModeloNuvem.Trim();
        // Opcionais — vazio é uma escolha válida (retranscrição visual fica indisponível).
        config.ModeloVisaoLocal = string.IsNullOrWhiteSpace(modelo.ModeloVisaoLocal) ? null : modelo.ModeloVisaoLocal.Trim();
        config.ModeloVisaoNuvem = string.IsNullOrWhiteSpace(modelo.ModeloVisaoNuvem) ? null : modelo.ModeloVisaoNuvem.Trim();
        config.TimeoutSegundos = modelo.TimeoutSegundos;
        config.TimeoutVisaoSegundos = modelo.TimeoutVisaoSegundos;
        await db.SaveChangesAsync();
    }
}
