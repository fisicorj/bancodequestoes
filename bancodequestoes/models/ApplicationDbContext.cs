using BancoQuestoes.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Data;

// Herdar de IdentityDbContext<ApplicationUser> (em vez de DbContext puro) integra o
// Identity ao mesmo banco/DbContext do resto do sistema.
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
    public DbSet<ProvaDisciplina> ProvasDisciplinas => Set<ProvaDisciplina>();
    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<AreaCurso> AreasCurso => Set<AreaCurso>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<QuestaoHistorico> QuestoesHistorico => Set<QuestaoHistorico>();
    public DbSet<MatrizReferencia> MatrizesReferencia => Set<MatrizReferencia>();
    public DbSet<ItemMatrizReferencia> ItensMatrizReferencia => Set<ItemMatrizReferencia>();

    // Exposto direto (além do skip-navigation Questao.ItensMatriz) porque o dashboard de
    // cobertura curricular precisa agrupar/contar vínculos sem carregar entidades inteiras.
    public DbSet<QuestaoItemMatriz> QuestoesItensMatriz => Set<QuestaoItemMatriz>();

    // Exposto direto (mesmo motivo acima): migration de backfill e QuestaoQueryService
    // precisam consultar/gravar vínculos em lote sem carregar entidades inteiras.
    public DbSet<QuestaoAreaCurso> QuestoesAreasCurso => Set<QuestaoAreaCurso>();

    public DbSet<CursoDisciplina> CursosDisciplinas => Set<CursoDisciplina>();

    // Configuração única/global de IA — sempre uma linha só, editável pela tela
    // /admin/configuracao-ia.
    public DbSet<ConfiguracaoIa> ConfiguracoesIa => Set<ConfiguracaoIa>();

    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<TurmaAluno> TurmasAlunos => Set<TurmaAluno>();
    public DbSet<AplicacaoProva> AplicacoesProva => Set<AplicacaoProva>();
    public DbSet<AcessoAlunoAplicacao> AcessosAlunoAplicacao => Set<AcessoAlunoAplicacao>();
    public DbSet<RespostaProvaOnline> RespostasProvaOnline => Set<RespostaProvaOnline>();
    public DbSet<RespostaQuestaoOnline> RespostasQuestaoOnline => Set<RespostaQuestaoOnline>();
    public DbSet<RespostaLacunaOnline> RespostasLacunaOnline => Set<RespostaLacunaOnline>();

    public DbSet<CartaoRespostaAplicacao> CartoesRespostaAplicacao => Set<CartaoRespostaAplicacao>();
    public DbSet<CartaoAlunoAplicacao> CartoesAlunoAplicacao => Set<CartaoAlunoAplicacao>();
    public DbSet<RespostaCartaoQuestao> RespostasCartaoQuestao => Set<RespostaCartaoQuestao>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // OBRIGATÓRIO chamar base.OnModelCreating primeiro: registra as tabelas do
        // Identity — esquecer isso quebra o Identity silenciosamente.
        base.OnModelCreating(builder);

        // TPT: cada classe da hierarquia vira sua própria tabela, ligada à tabela "pai"
        // (Questoes) por uma FK que também é PK.
        builder.Entity<Questao>().ToTable("Questoes");
        builder.Entity<QuestaoMultiplaEscolha>().ToTable("QuestoesMultiplaEscolha");
        builder.Entity<QuestaoDiscursiva>().ToTable("QuestoesDiscursivas");
        builder.Entity<QuestaoCertoErrado>().ToTable("QuestoesCertoErrado");
        builder.Entity<QuestaoAssociacao>().ToTable("QuestoesAssociacao");
        builder.Entity<QuestaoRespostaBreve>().ToTable("QuestoesRespostaBreve");
        builder.Entity<QuestaoNumerica>().ToTable("QuestoesNumericas");
        builder.Entity<QuestaoLacunas>().ToTable("QuestoesLacunas");

        // Força gravar o enum como texto ("MultiplaEscolha"...) em vez do inteiro
        // padrão, pra deixar o banco legível direto no psql.
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

        // Nullable (só ENADE preenche): sem HasDefaultValue/HasSentinel porque null já
        // é um valor válido direto (diferente de Prova.TipoEscopo abaixo).
        builder.Entity<Questao>()
            .Property(q => q.SecaoEnade)
            .HasConversion<string>();

        builder.Entity<Prova>()
            .Property(p => p.Tipo)
            .HasConversion<string>();

        builder.Entity<Prova>()
            .Property(p => p.TipoEscopo)
            .HasConversion<string>()
            .HasDefaultValue(TipoEscopoProva.Disciplina)
            // Silencia EFCore.Model.Validation[20601]: enum começa em 1, então o
            // default CLR (0) nunca é usado — só torna isso explícito pro EF.
            .HasSentinel(TipoEscopoProva.Disciplina);

        builder.Entity<ConfiguracaoIa>()
            .Property(c => c.Modo)
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

        // HasDefaultValue não é "padrão implícito" pro professor (a tela sempre exige
        // escolha) — é só pro banco saber o que fazer com linhas que já existiam antes desta coluna.
        builder.Entity<Instituicao>()
            .Property(i => i.SistemaPeriodos)
            .HasConversion<string>()
            .HasDefaultValue(SistemaPeriodos.Semestral);

        builder.Entity<QuestaoImagem>()
            .Property(i => i.Alinhamento)
            .HasConversion<string>();

        // Restrict explícito (auditoria de 09/09/2026): sem isso o EF cai na convenção
        // implícita de Cascade pra FK não-anulável, o que apagaria em cadeia todas as
        // Questões de um Assunto ao excluir o Assunto (e todos os Assuntos+Questões de uma
        // Disciplina ao excluir a Disciplina) sem aviso nenhum — silenciosamente, sem nem
        // passar pelo catch de DbUpdateException que DisciplinaService já tem pronto pra
        // barrar isso. Restrict faz o banco recusar a exclusão em vez de apagar o conteúdo.
        builder.Entity<Questao>()
            .HasOne(q => q.Assunto)
            .WithMany(a => a.Questoes)
            .HasForeignKey(q => q.AssuntoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Assunto>()
            .HasOne(a => a.Disciplina)
            .WithMany(d => d.Assuntos)
            .HasForeignKey(a => a.DisciplinaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Muitos-pra-muitos "de mão única": Tag não tem coleção de volta pra Questao,
        // então precisa do WithMany() vazio em vez da sintaxe curta.
        builder.Entity<Questao>()
            .HasMany(q => q.Tags)
            .WithMany()
            .UsingEntity(j => j.ToTable("QuestoesTags"));

        // Índice único comum é case-sensitive (deixaria "Pipeline"/"pipeline" passarem);
        // coluna gerada em minúsculas + índice nela garante unicidade real no banco.
        builder.Entity<Tag>(tag =>
        {
            tag.Property<string>("NomeNormalizado")
                .HasComputedColumnSql("lower(\"Nome\")", stored: true);

            tag.HasIndex("NomeNormalizado")
                .IsUnique();
        });

        // SetNull: excluir uma instituição não trava — o professor só perde o
        // vínculo e continua logável.
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Instituicao)
            .WithMany()
            .HasForeignKey(u => u.InstituicaoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Explícito (o EF adivinharia pelo nome da FK) pra documentar a intenção e
        // evitar surpresas numa renomeação futura.
        builder.Entity<AlternativaQuestao>()
            .HasOne(a => a.Questao)
            .WithMany(q => q.Alternativas)
            .HasForeignKey(a => a.QuestaoMultiplaEscolhaId);

        // Mesma lógica acima, pra Associação e Lacunas.
        builder.Entity<ParAssociacao>()
            .HasOne(p => p.Questao)
            .WithMany(q => q.Pares)
            .HasForeignKey(p => p.QuestaoAssociacaoId);

        builder.Entity<LacunaResposta>()
            .HasOne(l => l.Questao)
            .WithMany(q => q.Lacunas)
            .HasForeignKey(l => l.QuestaoLacunasId);

        // ProvaQuestao tem DUAS relações "muitos pra um" (Prova e Questao); o EF
        // precisa das FKs explícitas pra saber qual é qual.
        builder.Entity<ProvaQuestao>()
            .HasOne(pq => pq.Prova)
            .WithMany(p => p.ProvaQuestoes)
            .HasForeignKey(pq => pq.ProvaId);

        // Restrict (não Cascade): apagar uma questão vinculada a prova precisa
        // falhar de propósito, não encolher a prova silenciosamente.
        builder.Entity<ProvaQuestao>()
            .HasOne(pq => pq.Questao)
            .WithMany()
            .HasForeignKey(pq => pq.QuestaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Evita questão duplicada na mesma prova — regra garantida pelo banco.
        builder.Entity<ProvaQuestao>()
            .HasIndex(pq => new { pq.ProvaId, pq.QuestaoId })
            .IsUnique();

        // ProvaDisciplina -> Prova é Cascade (a linha só existe em função da prova);
        // -> Disciplina é Restrict (apagar Disciplina em uso precisa falhar).
        builder.Entity<ProvaDisciplina>()
            .HasOne(pd => pd.Prova)
            .WithMany(p => p.ProvaDisciplinas)
            .HasForeignKey(pd => pd.ProvaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProvaDisciplina>()
            .HasOne(pd => pd.Disciplina)
            .WithMany()
            .HasForeignKey(pd => pd.DisciplinaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mesma Disciplina não pode entrar duas vezes na mesma prova.
        builder.Entity<ProvaDisciplina>()
            .HasIndex(pd => new { pd.ProvaId, pd.DisciplinaId })
            .IsUnique();

        // Restrict: uma matriz usada por alguma prova não pode ser excluída, só
        // desativada, senão a prova perderia a referência silenciosamente.
        builder.Entity<Prova>()
            .HasOne(p => p.MatrizReferencia)
            .WithMany()
            .HasForeignKey(p => p.MatrizReferenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Curso opcional: apagar um curso não apaga as provas, só desvincula —
        // SetNull explícito pra exclusão nunca travar por engano.
        builder.Entity<Prova>()
            .HasOne(p => p.Curso)
            .WithMany()
            .HasForeignKey(p => p.CursoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mesmo raciocínio de Prova.Curso acima: SetNull explícito em vez de
        // confiar na convenção implícita do EF pra FK anulável.
        builder.Entity<Prova>()
            .HasOne(p => p.Disciplina)
            .WithMany()
            .HasForeignKey(p => p.DisciplinaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict (não SetNull): diferente de Prova, Turma sem Curso/Disciplina
        // não faz sentido — apagar um deles com turma vinculada precisa falhar.
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

        // Prova.Turma é opcional: apagar uma turma só desvincula, mesmo padrão do Curso acima.
        builder.Entity<Prova>()
            .HasOne(p => p.Turma)
            .WithMany()
            .HasForeignKey(p => p.TurmaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Cascade na Questão (sem ela o histórico não faz sentido); SetNull no autor
        // (excluído o usuário, a linha do histórico continua, só perde quem editou).
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

        // Mesma técnica de Tag.NomeNormalizado acima: evita duas AreaCurso
        // diferentes por variação de maiúsculas numa corrida de cadastros.
        builder.Entity<AreaCurso>(area =>
        {
            area.Property<string>("NomeNormalizado")
                .HasComputedColumnSql("lower(\"Nome\")", stored: true);

            area.HasIndex("NomeNormalizado").IsUnique();
        });

        // Restrict (não SetNull): uma AreaCurso "em uso" por algum Curso não pode ser
        // excluída silenciosamente — desativar (Ativo=false) é o caminho normal.
        builder.Entity<Curso>()
            .HasOne(c => c.AreaCurso)
            .WithMany()
            .HasForeignKey(c => c.AreaCursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Curso>().HasIndex(c => c.AreaCursoId);

        // MatrizReferencia.Curso é Restrict, mesmo padrão de Turma.CursoId: o histórico
        // de matrizes (inclusive Historicas) não pode sumir junto com o curso.
        builder.Entity<MatrizReferencia>()
            .HasOne(m => m.Curso)
            .WithMany()
            .HasForeignKey(m => m.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mesmo raciocínio Restrict acima, pro escopo nacional (ENADE/DCN).
        builder.Entity<MatrizReferencia>()
            .HasOne(m => m.AreaCurso)
            .WithMany()
            .HasForeignKey(m => m.AreaCursoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tipo/Status não são FK, então não ganham índice de graça pela convenção
        // do EF; CursoId/AreaCursoId já ganhariam, mas ficam explícitos por clareza.
        builder.Entity<MatrizReferencia>().HasIndex(m => m.CursoId);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.AreaCursoId);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Tipo);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Ano);
        builder.Entity<MatrizReferencia>().HasIndex(m => m.Status);

        // Restrict: apagar uma matriz com itens cadastrados falha de propósito;
        // desativar (Status/Ativo) é o caminho normal.
        builder.Entity<ItemMatrizReferencia>()
            .HasOne(i => i.MatrizReferencia)
            .WithMany(m => m.Itens)
            .HasForeignKey(i => i.MatrizReferenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.MatrizReferenciaId);
        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.Tipo);
        builder.Entity<ItemMatrizReferencia>().HasIndex(i => i.Codigo);

        // Único DENTRO da mesma matriz (não globalmente — "C01" pode repetir entre
        // matrizes diferentes), garantindo no banco a mesma regra checada na aplicação.
        builder.Entity<ItemMatrizReferencia>()
            .HasIndex(i => new { i.MatrizReferenciaId, i.Codigo })
            .IsUnique();

        // Classe de junção própria: UsingEntity dá skip-navigation + tabela nomeada.
        // Chave composta evita duplicidade; Restrict no Item, Cascade padrão na Questao.
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

        // Mesma técnica de Questao.ItensMatriz acima: Restrict do lado da AreaCurso,
        // Cascade padrão do lado da Questao, chave composta evita duplicidade.
        builder.Entity<Questao>()
            .HasMany(q => q.AreasCurso)
            .WithMany()
            .UsingEntity<QuestaoAreaCurso>(
                j => j.HasOne(qa => qa.AreaCurso)
                    .WithMany()
                    .HasForeignKey(qa => qa.AreaCursoId)
                    .OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne(qa => qa.Questao)
                    .WithMany()
                    .HasForeignKey(qa => qa.QuestaoId),
                j =>
                {
                    j.ToTable("QuestoesAreasCurso");
                    j.HasKey(qa => new { qa.QuestaoId, qa.AreaCursoId });
                    j.HasIndex(qa => qa.AreaCursoId);
                });

        // Restrict, mesmo padrão de Turma.CursoId/DisciplinaId: não desvincula
        // silenciosamente a grade curricular.
        builder.Entity<CursoDisciplina>()
            .HasOne(cd => cd.Curso)
            .WithMany()
            .HasForeignKey(cd => cd.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CursoDisciplina>()
            .HasOne(cd => cd.Disciplina)
            .WithMany()
            .HasForeignKey(cd => cd.DisciplinaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mesma Disciplina não pode entrar duas vezes na grade do mesmo Curso.
        builder.Entity<CursoDisciplina>()
            .HasIndex(cd => new { cd.CursoId, cd.DisciplinaId })
            .IsUnique();

        // --- Prova online (Aluno/TurmaAluno/AplicacaoProva/RespostaProvaOnline) ---

        builder.Entity<AplicacaoProva>()
            .Property(a => a.Status)
            .HasConversion<string>();

        builder.Entity<RespostaProvaOnline>()
            .Property(r => r.Status)
            .HasConversion<string>();

        // Nulo enquanto EmAndamento (ver comentário no enum) — sem HasDefaultValue/HasSentinel,
        // mesmo padrão de Questao.SecaoEnade, já que null é um valor válido direto.
        builder.Entity<RespostaProvaOnline>()
            .Property(r => r.MotivoEncerramento)
            .HasConversion<string>();

        // SetNull: mesmo padrão de ApplicationUser.InstituicaoId — excluir a instituição não
        // trava, só desvincula o Aluno.
        builder.Entity<Aluno>()
            .HasOne(a => a.Instituicao)
            .WithMany()
            .HasForeignKey(a => a.InstituicaoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict nos dois lados, mesmo padrão de CursoDisciplina: matrícula não desvincula
        // silenciosamente ao excluir Turma ou Aluno — desativar (Ativa=false) é o caminho normal.
        builder.Entity<TurmaAluno>()
            .HasOne(ta => ta.Turma)
            .WithMany()
            .HasForeignKey(ta => ta.TurmaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TurmaAluno>()
            .HasOne(ta => ta.Aluno)
            .WithMany(a => a.TurmaAlunos)
            .HasForeignKey(ta => ta.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mesmo Aluno não pode matricular duas vezes na mesma Turma.
        builder.Entity<TurmaAluno>()
            .HasIndex(ta => new { ta.TurmaId, ta.AlunoId })
            .IsUnique();

        // Restrict nos dois lados: uma Prova ou Turma com aplicação online não pode ser
        // excluída silenciosamente, mesmo raciocínio de ProvaQuestao.Questao.
        builder.Entity<AplicacaoProva>()
            .HasOne(a => a.Prova)
            .WithMany()
            .HasForeignKey(a => a.ProvaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AplicacaoProva>()
            .HasOne(a => a.Turma)
            .WithMany()
            .HasForeignKey(a => a.TurmaId)
            .OnDelete(DeleteBehavior.Restrict);

        // CriadoPorId sem configuração explícita (mesmo padrão de Turma/Prova.CriadoPorId):
        // FK opcional, convenção implícita do EF já é SetNull.

        // AcessoAlunoAplicacao: um código de acesso POR ALUNO (não mais um único código
        // compartilhado pela turma inteira). Cascade na AplicacaoProva (diferente de
        // RespostaProvaOnline abaixo!) — o código em si não é dado de aluno nenhum, é só
        // gerado automaticamente pra todo mundo matriculado na hora de criar a aplicação,
        // então excluir a aplicação apaga os códigos junto sem bloquear a exclusão.
        builder.Entity<AcessoAlunoAplicacao>()
            .HasOne(a => a.AplicacaoProva)
            .WithMany(a => a.Acessos)
            .HasForeignKey(a => a.AplicacaoProvaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AcessoAlunoAplicacao>()
            .HasOne(a => a.Aluno)
            .WithMany()
            .HasForeignKey(a => a.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Só um código por Aluno em cada AplicacaoProva.
        builder.Entity<AcessoAlunoAplicacao>()
            .HasIndex(a => new { a.AplicacaoProvaId, a.AlunoId })
            .IsUnique();

        // Código de acesso é o que o aluno digita — precisa ser único no sistema todo.
        builder.Entity<AcessoAlunoAplicacao>()
            .HasIndex(a => a.CodigoAcesso)
            .IsUnique();

        // Restrict nos dois lados, mesma lógica de AplicacaoProva acima.
        builder.Entity<RespostaProvaOnline>()
            .HasOne(r => r.AplicacaoProva)
            .WithMany(a => a.Respostas)
            .HasForeignKey(r => r.AplicacaoProvaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RespostaProvaOnline>()
            .HasOne(r => r.Aluno)
            .WithMany()
            .HasForeignKey(r => r.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Só uma tentativa por Aluno em cada AplicacaoProva (regra de negócio: sem resubmissão).
        builder.Entity<RespostaProvaOnline>()
            .HasIndex(r => new { r.AplicacaoProvaId, r.AlunoId })
            .IsUnique();

        // Cascade: a resposta a uma questão só existe em função da tentativa (mesmo padrão
        // de QuestaoHistorico->Questao); Restrict na Questao pra não perder histórico de
        // resposta numa exclusão silenciosa da questão.
        builder.Entity<RespostaQuestaoOnline>()
            .HasOne(r => r.RespostaProvaOnline)
            .WithMany(r => r.Respostas)
            .HasForeignKey(r => r.RespostaProvaOnlineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RespostaQuestaoOnline>()
            .HasOne(r => r.Questao)
            .WithMany()
            .HasForeignKey(r => r.QuestaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade: mesma ideia de LacunaResposta->QuestaoLacunas, a lacuna respondida só
        // existe em função da questão respondida.
        builder.Entity<RespostaLacunaOnline>()
            .HasOne(r => r.RespostaQuestaoOnline)
            .WithMany(r => r.RespostasLacunas)
            .HasForeignKey(r => r.RespostaQuestaoOnlineId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Cartão resposta (leitura de bolhas de prova impressa) ---

        builder.Entity<CartaoAlunoAplicacao>()
            .Property(c => c.Status)
            .HasConversion<string>();

        // Restrict nos dois lados: mesmo raciocínio de AplicacaoProva.Prova/Turma —
        // Prova/Turma com aplicação de cartão resposta não pode ser excluída silenciosamente.
        builder.Entity<CartaoRespostaAplicacao>()
            .HasOne(c => c.Prova)
            .WithMany()
            .HasForeignKey(c => c.ProvaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CartaoRespostaAplicacao>()
            .HasOne(c => c.Turma)
            .WithMany()
            .HasForeignKey(c => c.TurmaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade (não Restrict!): mesmo raciocínio do AcessoAlunoAplicacao da prova online —
        // o cartão em si (token/QR) é gerado automaticamente pra todo mundo matriculado na
        // hora de criar a aplicação, então excluir a aplicação apaga os cartões junto.
        builder.Entity<CartaoAlunoAplicacao>()
            .HasOne(c => c.CartaoRespostaAplicacao)
            .WithMany(c => c.Cartoes)
            .HasForeignKey(c => c.CartaoRespostaAplicacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CartaoAlunoAplicacao>()
            .HasOne(c => c.Aluno)
            .WithMany()
            .HasForeignKey(c => c.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Só um cartão por Aluno em cada CartaoRespostaAplicacao.
        builder.Entity<CartaoAlunoAplicacao>()
            .HasIndex(c => new { c.CartaoRespostaAplicacaoId, c.AlunoId })
            .IsUnique();

        // Token é o que o QR Code carrega — precisa ser único no sistema todo, mesma lógica
        // do CodigoAcesso da prova online.
        builder.Entity<CartaoAlunoAplicacao>()
            .HasIndex(c => c.Token)
            .IsUnique();

        // Cascade: a resposta de uma questão só existe em função do cartão do aluno (mesmo
        // padrão de RespostaQuestaoOnline->RespostaProvaOnline); Restrict na Questao pra não
        // perder histórico de leitura numa exclusão silenciosa da questão.
        builder.Entity<RespostaCartaoQuestao>()
            .HasOne(r => r.CartaoAlunoAplicacao)
            .WithMany(c => c.Respostas)
            .HasForeignKey(r => r.CartaoAlunoAplicacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RespostaCartaoQuestao>()
            .HasOne(r => r.Questao)
            .WithMany()
            .HasForeignKey(r => r.QuestaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
