using BancoQuestoes.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Data;

// IdentityDbContext<ApplicationUser> já traz DbSets prontos para Users, Roles,
// UserClaims, etc. Herdar dele em vez de DbContext puro é o que integra
// o Identity ao mesmo banco/mesmo DbContext do resto do sistema.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<Assunto> Assuntos => Set<Assunto>();
    public DbSet<Questao> Questoes => Set<Questao>();
    public DbSet<AlternativaQuestao> AlternativasQuestao => Set<AlternativaQuestao>();
    public DbSet<QuestaoImagem> QuestoesImagens => Set<QuestaoImagem>();
    public DbSet<ParAssociacao> ParesAssociacao => Set<ParAssociacao>();
    public DbSet<LacunaResposta> LacunasRespostas => Set<LacunaResposta>();
    public DbSet<Prova> Provas => Set<Prova>();
    public DbSet<ProvaQuestao> ProvasQuestoes => Set<ProvaQuestao>();
    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<AreaCurso> AreasCurso => Set<AreaCurso>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<QuestaoHistorico> QuestoesHistorico => Set<QuestaoHistorico>();
    public DbSet<MatrizReferencia> MatrizesReferencia => Set<MatrizReferencia>();
    public DbSet<ItemMatrizReferencia> ItensMatrizReferencia => Set<ItemMatrizReferencia>();

    // Também exposto direto (além do skip-navigation Questao.ItensMatriz)
    // porque telas como o dashboard de cobertura curricular (ver
    // MatrizReferenciaService) precisam agrupar/contar vínculos sem carregar
    // as Questoes/Itens inteiros pra memória.
    public DbSet<QuestaoItemMatriz> QuestoesItensMatriz => Set<QuestaoItemMatriz>();

    // OnModelCreating é onde a "Fluent API" entra: uma forma de configurar
    // detalhes do mapeamento objeto-banco que não dá para (ou não é claro)
    // expressar só com atributos nas classes. Aqui é onde definimos o TPT.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // OBRIGATÓRIO chamar base.OnModelCreating primeiro: é isso que registra
        // as tabelas do Identity (AspNetUsers, AspNetRoles, etc.). Esquecer essa
        // linha é um erro clássico que quebra o Identity silenciosamente.
        base.OnModelCreating(builder);

        // TPT (Table-Per-Type): cada classe da hierarquia vira sua própria
        // tabela. ToTable("Questoes") na classe base define o nome da tabela
        // "pai"; cada ToTable() nas subclasses define a tabela "filha",
        // ligada por uma FK que também é PK (relação 1-para-1).
        builder.Entity<Questao>().ToTable("Questoes");
        builder.Entity<QuestaoMultiplaEscolha>().ToTable("QuestoesMultiplaEscolha");
        builder.Entity<QuestaoDiscursiva>().ToTable("QuestoesDiscursivas");
        builder.Entity<QuestaoCertoErrado>().ToTable("QuestoesCertoErrado");
        builder.Entity<QuestaoAssociacao>().ToTable("QuestoesAssociacao");
        builder.Entity<QuestaoRespostaBreve>().ToTable("QuestoesRespostaBreve");
        builder.Entity<QuestaoNumerica>().ToTable("QuestoesNumericas");
        builder.Entity<QuestaoLacunas>().ToTable("QuestoesLacunas");

        // Sem essa linha, o EF Core mapearia Questao.TipoQuestao para uma coluna
        // do tipo integer (o valor numérico do enum). Isso força a gravar como
        // texto ("MultiplaEscolha", "Discursiva"...), o que deixa o banco
        // legível se você um dia for olhar os dados direto no psql.
        builder.Entity<Questao>()
            .Property(q => q.TipoQuestao)
            .HasConversion<string>();

        builder.Entity<Questao>()
            .Property(q => q.Dificuldade)
            .HasConversion<string>();

        builder.Entity<Questao>()
            .Property(q => q.Visibilidade)
            .HasConversion<string>();

        builder.Entity<Questao>()
            .Property(q => q.Bloom)
            .HasConversion<string>();

        builder.Entity<Questao>()
            .Property(q => q.Origem)
            .HasConversion<string>();

        builder.Entity<Prova>()
            .Property(p => p.Tipo)
            .HasConversion<string>();

        builder.Entity<MatrizReferencia>()
            .Property(m => m.Tipo)
            .HasConversion<string>();

        builder.Entity<MatrizReferencia>()
            .Property(m => m.Status)
            .HasConversion<string>();

        builder.Entity<ItemMatrizReferencia>()
            .Property(i => i.Tipo)
            .HasConversion<string>();

        // HasDefaultValue aqui não é "padrão implícito" pro professor — a
        // tela nunca deixa salvar sem escolher (ver InstituicaoInput). É só
        // pro banco saber o que fazer com as linhas que já existiam ANTES
        // dessa coluna existir: viram Semestral, que era o comportamento
        // implícito de todo o sistema até agora (só "Semestre" existia).
        // Editar uma dessas instituições já mostra Semestral pré-selecionado
        // no formulário, não força escolher nada de novo.
        builder.Entity<Instituicao>()
            .Property(i => i.SistemaPeriodos)
            .HasConversion<string>()
            .HasDefaultValue(SistemaPeriodos.Semestral);

        builder.Entity<QuestaoImagem>()
            .Property(i => i.Alinhamento)
            .HasConversion<string>();

        // Muitos-para-muitos "de mão única": Questao.Tags existe, mas Tag não
        // tem uma coleção de volta pra Questao (não precisamos navegar
        // "quais questões usam essa tag" a partir da Tag em nenhuma tela) —
        // o EF Core aceita isso desde a v5, só não dá pra usar a sintaxe curta
        // (HasMany().WithMany(x => x.Algo)), precisa do WithMany() vazio.
        builder.Entity<Questao>()
            .HasMany(q => q.Tags)
            .WithMany()
            .UsingEntity(j => j.ToTable("QuestoesTags"));

        // Nome da tag único de verdade, sem depender de "não deveria acontecer
        // na prática": o ILIKE em QuestaoService.ResolverTagsAsync (busca antes
        // de criar) só reduz a chance de colisão, não elimina a corrida entre
        // duas requisições concorrentes tentando criar "Pipeline" e "pipeline"
        // ao mesmo tempo — um índice único comum no Postgres é case-sensitive,
        // então os dois passariam. Uma coluna gerada (stored) com o nome em
        // minúsculas + índice único NELA garante a unicidade no banco, não só
        // na aplicação.
        builder.Entity<Tag>(tag =>
        {
            tag.Property<string>("NomeNormalizado")
                .HasComputedColumnSql("lower(\"Nome\")", stored: true);

            tag.HasIndex("NomeNormalizado")
                .IsUnique();
        });

        // Instituição do professor é opcional e não pode travar a exclusão de
        // uma instituição (SetNull): o professor só perde o vínculo, continua
        // logável, só "Institucional" passa a não valer mais nada pra ele até
        // escolher outra.
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Instituicao)
            .WithMany()
            .HasForeignKey(u => u.InstituicaoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configura a FK "AlternativaQuestao -> QuestaoMultiplaEscolha" explicitamente.
        // O EF Core provavelmente adivinharia isso sozinho pelo nome
        // "QuestaoMultiplaEscolhaId", mas ser explícito aqui documenta a intenção
        // e evita surpresas se você renomear algo no futuro.
        builder.Entity<AlternativaQuestao>()
            .HasOne(a => a.Questao)
            .WithMany(q => q.Alternativas)
            .HasForeignKey(a => a.QuestaoMultiplaEscolhaId);

        // Mesma lógica da FK de AlternativaQuestao acima, só que pra Associação
        // (Pares) e Lacunas (Lacunas) — cada uma é um "muitos" ligado a UMA
        // subclasse específica de Questao, então precisa da FK explícita.
        builder.Entity<ParAssociacao>()
            .HasOne(p => p.Questao)
            .WithMany(q => q.Pares)
            .HasForeignKey(p => p.QuestaoAssociacaoId);

        builder.Entity<LacunaResposta>()
            .HasOne(l => l.Questao)
            .WithMany(q => q.Lacunas)
            .HasForeignKey(l => l.QuestaoLacunasId);

        // Muitos-para-muitos com payload (ProvaQuestao): configuramos as duas
        // FKs manualmente porque ProvaQuestao tem DUAS relações "muitos para um"
        // (uma com Prova, outra com Questao), e o EF Core precisa saber
        // qual é qual — ele não adivinha isso sem ajuda.
        builder.Entity<ProvaQuestao>()
            .HasOne(pq => pq.Prova)
            .WithMany(p => p.ProvaQuestoes)
            .HasForeignKey(pq => pq.ProvaId);

        // Restrict (em vez do Cascade padrão): apagar uma questão que ainda está
        // vinculada a alguma prova precisa falhar de propósito, não apagar em
        // cascata o vínculo e silenciosamente encolher a prova já salva.
        builder.Entity<ProvaQuestao>()
            .HasOne(pq => pq.Questao)
            .WithMany()
            .HasForeignKey(pq => pq.QuestaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Evita duas questões na mesma prova na mesma posição, e evita a mesma
        // questão duplicada na mesma prova. Índice único = regra de negócio
        // garantida pelo próprio banco, não só pelo código C#.
        builder.Entity<ProvaQuestao>()
            .HasIndex(pq => new { pq.ProvaId, pq.QuestaoId })
            .IsUnique();

        // Curso é opcional (FK anulável): apagar um curso não pode apagar as
        // provas que o referenciam, só desvincular (o padrão para FK anulável já
        // seria Restrict, mas deixamos explícito SetNull para o comportamento
        // ser óbvio e a exclusão nunca travar por engano).
        builder.Entity<Prova>()
            .HasOne(p => p.Curso)
            .WithMany()
            .HasForeignKey(p => p.CursoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Turma.CursoId/DisciplinaId são Restrict (não SetNull): diferente de
        // Prova, uma Turma SEM Curso ou SEM Disciplina não faz sentido — apagar
        // um Curso/Disciplina que ainda tem turma vinculada precisa falhar de
        // propósito, igual já fazemos com ProvaQuestao -> Questao.
        builder.Entity<Turma>()
            .HasOne(t => t.Curso)
            .WithMany()
            .HasForeignKey(t => t.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Turma>()
            .HasOne(t => t.Disciplina)
            .WithMany()
            .HasForeignKey(t => t.DisciplinaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Já Prova.Turma é opcional: apagar uma turma não pode apagar as provas
        // que a referenciam, só desvincular — mesmo padrão do Prova.Curso acima.
        builder.Entity<Prova>()
            .HasOne(p => p.Turma)
            .WithMany()
            .HasForeignKey(p => p.TurmaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Histórico de edição (auditoria): Cascade na Questão, porque sem a
        // questão o histórico sozinho não faz sentido (e ExcluirAsync só
        // deixa apagar de verdade quem nunca foi usado em prova, então isso
        // não acontece o tempo todo). Já o autor é SetNull, mesmo padrão de
        // ApplicationUser.Instituicao acima: se o usuário for excluído, a
        // linha do histórico continua existindo, só perde quem fez a edição.
        builder.Entity<QuestaoHistorico>(h =>
        {
            h.HasOne(x => x.Questao)
                .WithMany()
                .HasForeignKey(x => x.QuestaoId)
                .OnDelete(DeleteBehavior.Cascade);

            h.HasOne(x => x.Usuario)
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            h.HasIndex(x => x.QuestaoId);
        });

        // Área de Curso (catálogo nacional, 2ª rodada de revisão) ------------
        //
        // Nome único de verdade no banco (mesma técnica de Tag.NomeNormalizado
        // acima): evita "Engenharia de Computação" e "engenharia de
        // computação" virando duas AreaCurso diferentes por uma corrida entre
        // dois cadastros concorrentes — a checagem em AreaCursoService só
        // reduz a chance, o índice único é quem garante.
        builder.Entity<AreaCurso>(area =>
        {
            area.Property<string>("NomeNormalizado")
                .HasComputedColumnSql("lower(\"Nome\")", stored: true);

            area.HasIndex("NomeNormalizado").IsUnique();
        });

        // Curso -> AreaCurso é opcional e SetNull: apagar uma AreaCurso não
        // pode apagar os Cursos que apontam pra ela, só desvincular (o
        // curso continua existindo normalmente, só sem Área até o professor
        // escolher outra) — mesmo raciocínio de Prova.Curso/Prova.Turma.
        builder.Entity<Curso>()
            .HasOne(c => c.AreaCurso)
            .WithMany()
            .HasForeignKey(c => c.AreaCursoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Curso>().HasIndex(c => c.AreaCursoId);

        // Matriz de Referência Curricular / ENADE ----------------------------

        // Questao.Curso é opcional, mesmo padrão de Prova.Curso acima: apagar
        // um curso não pode apagar questões, só desvincular (e a questão
        // continua existindo/editável normalmente, só sem Alinhamento
        // Curricular até o professor escolher outro curso).
        builder.Entity<Questao>()
            .HasOne(q => q.Curso)
            .WithMany()
            .HasForeignKey(q => q.CursoId)
            .OnDelete(DeleteBehavior.SetNull);

        // MatrizReferencia.Curso é Restrict, mesmo padrão de Turma.CursoId/
        // DisciplinaId: o histórico de matrizes de um curso (inclusive as
        // Historicas — item 21 do pedido, versionamento nunca apaga edição
        // antiga) não pode sumir silenciosamente junto com o curso. Agora
        // opcional (CursoId é int?) — só preenchido pra Tipo PPC/
        // Institucional/Outro (ver comentário no model).
        builder.Entity<MatrizReferencia>()
            .HasOne(m => m.Curso)
            .WithMany()
            .HasForeignKey(m => m.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        // MatrizReferencia.AreaCurso: mesmo raciocínio Restrict acima, só
        // que pro escopo nacional (ENADE/DCN) — apagar uma AreaCurso que
        // ainda tem matriz cadastrada precisa falhar de propósito.
        builder.Entity<MatrizReferencia>()
            .HasOne(m => m.AreaCurso)
            .WithMany()
            .HasForeignKey(m => m.AreaCursoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices sugeridos no pedido (item 27) — Tipo/Status não são FK
        // (são só classificação, igual Origem/Bloom em Questao), então não
        // vêm de graça pela convenção do EF; CursoId/AreaCursoId já ganhariam
        // índice só por serem FK, mas ficam explícitos aqui por clareza.
        builder.Entity<MatrizReferencia>().HasIndex(m => m.CursoId);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.AreaCursoId);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Tipo);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Ano);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Status);

        // ItemMatrizReferencia.MatrizReferencia é Restrict: apagar uma
        // matriz que ainda tem itens cadastrados precisa falhar de
        // propósito (MatrizReferenciaService.ExcluirAsync trata isso com
        // mensagem amigável, mesmo padrão de Curso/Turma/diretrizes antes
        // dele) — na prática, desativar (Status/Ativo) é o caminho normal;
        // excluir de verdade só vale pra matriz/item que nunca foi usado.
        builder.Entity<ItemMatrizReferencia>()
            .HasOne(i => i.MatrizReferencia)
            .WithMany(m => m.Itens)
            .HasForeignKey(i => i.MatrizReferenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.MatrizReferenciaId);
        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.Tipo);
        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.Codigo);

        // Código único DENTRO da mesma matriz (não globalmente — "C01" pode
        // se repetir em matrizes diferentes, ex.: ENADE 2023 e ENADE 2026
        // convivendo como Historica/Ativa) — garante no banco a mesma regra
        // que MatrizReferenciaService.ValidarItemAsync já checa na aplicação.
        builder.Entity<ItemMatrizReferencia>()
            .HasIndex(i => new { i.MatrizReferenciaId, i.Codigo })
            .IsUnique();

        // Muitos-para-muitos Questao <-> ItemMatrizReferencia via classe de
        // junção própria (QuestaoItemMatriz, não implícita como Tags acima)
        // — UsingEntity<QuestaoItemMatriz> permite ter as duas coisas ao
        // mesmo tempo: o skip-navigation curto (Questao.ItensMatriz, usado
        // pela UI) E uma tabela de junção real e nomeada
        // (QuestoesItensMatriz), que pode ganhar colunas próprias no futuro
        // (peso, origem de uma sugestão de IA) sem remodelar nada.
        //
        // Chave primária composta (QuestaoId + ItemMatrizReferenciaId), não
        // um Id substituto: além de ser a chave natural do vínculo, isso já
        // garante sozinho a não-duplicidade pedida no item 4 (a mesma
        // questão não pode ficar ligada duas vezes ao mesmo item) — sem
        // precisar de um índice único separado.
        //
        // Restrict do lado do Item: não deixa apagar um item ainda vinculado
        // a questões. Do lado da Questao fica o Cascade padrão do EF: apagar
        // uma questão remove só as linhas de vínculo dela, inofensivo (o
        // item da matriz em si continua existindo).
        builder.Entity<Questao>()
            .HasMany(q => q.ItensMatriz)
            .WithMany()
            .UsingEntity<QuestaoItemMatriz>(
                j => j.HasOne(qi => qi.ItemMatrizReferencia)
                    .WithMany()
                    .HasForeignKey(qi => qi.ItemMatrizReferenciaId)
                    .OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne(qi => qi.Questao)
                    .WithMany()
                    .HasForeignKey(qi => qi.QuestaoId),
                j =>
                {
                    j.ToTable("QuestoesItensMatriz");
                    j.HasKey(qi => new { qi.QuestaoId, qi.ItemMatrizReferenciaId });
                    j.HasIndex(qi => qi.ItemMatrizReferenciaId);
                });
    }
}
