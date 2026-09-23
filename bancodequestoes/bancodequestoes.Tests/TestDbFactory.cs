using BancoQuestoes.Data;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BancoQuestoes.Tests;

// Fábrica de ApplicationDbContext em memória (provider InMemory do EF Core)
// pros testes de Escopo — um banco novo, isolado, por chamada (nome de
// database = Guid novo), pra um teste nunca ver dado de outro mesmo rodando
// em paralelo.
//
// IMPORTANTE (documentado de novo aqui, junto do csproj): o provider
// InMemory não é o Npgsql de produção — não traduz EF.Functions.ILike, não
// aplica FKs/constraints reais, e alguns comportamentos de LINQ->SQL podem
// divergir sutilmente do Postgres real. Os métodos exercitados por estes
// testes (QuestaoQueryService.ObterCandidatasAsync/
// ValidarQuestaoIdsNoEscopoAsync, ProvaService.CriarAsync/AtualizarAsync/
// GerarAsync/DiagnosticarAsync, GeradorProvaService.*) não usam ILike nem
// dependem de comportamento específico do Postgres — mas isto NÃO substitui
// rodar contra Postgres real ao menos uma vez antes de produção (ver
// pendência no relatório final).
//
// ConfigureWarnings ignorando TransactionIgnoredWarning: alguns Services
// (ex.: AreaCursoService.MesclarAsync, MatrizReferenciaService.AtualizarAsync)
// abrem uma transação explícita (db.Database.BeginTransactionAsync()) — real
// e necessária contra o Postgres de produção, mas o provider InMemory não
// suporta transação nenhuma e, por padrão, TRANSFORMA esse "não suporto" num
// erro (Throw) em vez de só ignorar. Sem isso, qualquer teste que exercite
// um método transacional quebra com
// "TransactionIgnoredWarning"/InvalidOperationException, mesmo o código
// estando correto — silenciar é o próprio fix recomendado pela mensagem de
// erro do EF Core, não um jeito de esconder um problema real: os testes
// continuam verificando o ESTADO FINAL dos dados normalmente, só não testam
// atomicidade entre múltiplos SaveChanges (isso só um teste contra Postgres
// real cobre).
public static class TestDbFactory
{
    public static ApplicationDbContext Criar()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}

// Construtores de entidade mínimos, só com os campos que os testes de
// Escopo realmente precisam — sempre Visibilidade = Compartilhada (pra não
// precisar seedar ApplicationUser/Instituicao só pra passar em
// VisivelPara) e Ativa = true (senão some do pool por outro motivo,
// mascarando o que o teste realmente quer verificar).
// SugestaoIaService "desligado" pros testes — QuestaoService passou a
// depender dele (ver ClassificarBloomAutomaticamenteAsync com usarIa=true),
// mas nenhum teste aqui tem um Ollama de verdade rodando. Habilitada=false
// faz SugerirAsync/SugerirBloomAsync devolver null sem nunca tocar a rede,
// então o HttpClient "vazio" abaixo nunca chega a ser usado de verdade.
//
// SugestaoIaService agora lê a config via ConfiguracaoIaService (banco, ver
// Models/ConfiguracaoIa.cs), não mais via IOptions direto — por isso recebe
// o "db" do teste, só pra montar esse ConfiguracaoIaService por baixo; o
// IOptions<SugestaoIaOptions> com Habilitada=false ainda é o que efetivamente
// desliga, semeando a linha única de configuração na primeira leitura.
public static class TestFakes
{
    public static SugestaoIaService SugestaoIaDesativada(ApplicationDbContext db) => new(
        new HttpClient(),
        new OllamaClient(new HttpClient(), NullLogger<OllamaClient>.Instance),
        new ConfiguracaoIaService(db, Options.Create(new SugestaoIaOptions { Habilitada = false })));

    // Monta MatrizReferenciaService com as mesmas dependências reais de
    // Program.cs — MatrizReferenciaService foi dividido em Services menores
    // (separação de responsabilidades: MatrizAcessoService/
    // ItemMatrizReferenciaService/ItemMatrizVinculoService), então o
    // construtor passou a pedir esses colaboradores em vez de só "db".
    public static MatrizReferenciaService NovaMatrizReferenciaService(ApplicationDbContext db)
    {
        var acesso = new MatrizAcessoService(db);
        return new MatrizReferenciaService(
            db,
            acesso,
            new ItemMatrizReferenciaService(db, acesso),
            new ItemMatrizVinculoService(db));
    }

    // Monta QuestaoService com as mesmas dependências reais de Program.cs
    // (só trocando SugestaoIaService pelo fake desligado acima) — QuestaoService
    // foi dividido em Services menores (separação de responsabilidades:
    // QuestaoCurricularService/QuestaoImagemService/QuestaoTagService/
    // QuestaoHistoricoService), então o construtor passou a pedir esses
    // colaboradores em vez de só MatrizReferenciaService/SugestaoIaService.
    public static QuestaoService NovoQuestaoService(ApplicationDbContext db)
    {
        var matrizService = NovaMatrizReferenciaService(db);
        return new QuestaoService(
            db,
            new QuestaoCurricularService(db, matrizService),
            new QuestaoImagemService(db),
            new QuestaoTagService(db),
            new QuestaoHistoricoService(db),
            SugestaoIaDesativada(db));
    }
}

public static class TestSeed
{
    public static Disciplina Disciplina(string nome) => new() { Nome = nome };

    public static Assunto Assunto(string nome, int disciplinaId) => new()
    {
        Nome = nome,
        DisciplinaId = disciplinaId,
    };

    public static Curso Curso(string nome, int instituicaoId = 0, int? areaCursoId = null) => new()
    {
        Nome = nome,
        InstituicaoId = instituicaoId,
        AreaCursoId = areaCursoId,
    };

    public static AreaCurso AreaCurso(string nome) => new()
    {
        Nome = nome,
        Ativo = true,
    };

    public static CursoDisciplina CursoDisciplina(int cursoId, int disciplinaId) => new()
    {
        CursoId = cursoId,
        DisciplinaId = disciplinaId,
        Ativa = true,
    };

    public static Turma Turma(string nome, int cursoId, int disciplinaId) => new()
    {
        Nome = nome,
        CursoId = cursoId,
        DisciplinaId = disciplinaId,
    };

    public static QuestaoMultiplaEscolha Questao(
        string enunciado,
        int assuntoId,
        Dificuldade dificuldade = Dificuldade.Media,
        TipoQuestao tipo = TipoQuestao.MultiplaEscolha) => new()
    {
        Enunciado = enunciado,
        AssuntoId = assuntoId,
        Dificuldade = dificuldade,
        TipoQuestao = tipo,
        Visibilidade = VisibilidadeQuestao.Compartilhada,
        Ativa = true,
        RespostaCorreta = 'A',
    };

    public static MatrizReferencia Matriz(
        string nome, int cursoId, TipoMatrizReferencia tipo = TipoMatrizReferencia.ENADE, int? ano = null) => new()
    {
        Nome = nome,
        CursoId = cursoId,
        Tipo = tipo,
        Ano = ano,
        Status = StatusMatrizReferencia.Ativa,
    };

    public static ItemMatrizReferencia Item(
        string codigo, string titulo, int matrizReferenciaId, TipoItemMatriz tipo = TipoItemMatriz.Competencia) => new()
    {
        Codigo = codigo,
        Titulo = titulo,
        MatrizReferenciaId = matrizReferenciaId,
        Tipo = tipo,
        Ativo = true,
    };
}
