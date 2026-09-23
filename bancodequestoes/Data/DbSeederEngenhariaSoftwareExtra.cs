using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Estende a disciplina "Engenharia de Software" (já criada por
// DbSeeder.SeedEngenhariaDeSoftwareAsync) com Assuntos novos.
public static class DbSeederEngenhariaSoftwareExtra
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Engenharia de Software");

        var arquitetura = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Arquitetura de Software");
        var agil = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Gestão Ágil de Projetos");
        var devops = DbSeeder.ObterOuCriarAssunto(db, disciplina, "DevOps e Integração Contínua");
        var manutencao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Manutenção e Evolução de Software");

        var candidatas = new List<Questao>
        {
            // ---------------- Arquitetura de Software ----------------
            new QuestaoMultiplaEscolha
            {
                Assunto = arquitetura,
                Enunciado = "Na Arquitetura em Camadas (Layered Architecture), a comunicação típica entre as camadas segue qual princípio?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Cada camada só se comunica diretamente com a camada imediatamente adjacente" },
                    new() { Letra = 'B', Texto = "Todas as camadas se comunicam livremente entre si, sem restrição" },
                    new() { Letra = 'C', Texto = "Só a camada mais alta pode se comunicar com o banco de dados" },
                    new() { Letra = 'D', Texto = "As camadas nunca trocam dados entre si" },
                },
            },
            new QuestaoMultiplaEscolha
            {
                Assunto = arquitetura,
                Enunciado = "Em uma arquitetura de Microsserviços, cada serviço tipicamente:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Compartilha obrigatoriamente o mesmo banco de dados com todos os outros" },
                    new() { Letra = 'B', Texto = "É independentemente implantável e responsável por uma capacidade de negócio específica" },
                    new() { Letra = 'C', Texto = "Precisa ser escrito na mesma linguagem de programação que os demais" },
                    new() { Letra = 'D', Texto = "Não pode se comunicar com outros serviços" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = arquitetura,
                Enunciado = "Uma vantagem da arquitetura Monolítica sobre a de Microsserviços é a simplicidade de implantação (deploy) de uma aplicação pequena/média, por não exigir orquestração de múltiplos serviços.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = arquitetura,
                Enunciado = "O padrão arquitetural MVC (Model-View-Controller) separa a aplicação em três componentes principais: dados/regras de negócio (Model), apresentação (View) e controle de fluxo (Controller).",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = arquitetura,
                Enunciado = "Como se chama o estilo arquitetural em que sistemas se comunicam trocando mensagens/eventos de forma assíncrona, geralmente através de um barramento ou broker de mensagens?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Arquitetura orientada a eventos (Event-Driven Architecture)",
            },
            new QuestaoDiscursiva
            {
                Assunto = arquitetura,
                Enunciado = "Cite duas vantagens e duas desvantagens de adotar uma arquitetura de Microsserviços em vez de uma arquitetura Monolítica.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Vantagens: (1) escalabilidade independente de cada serviço conforme sua própria demanda; (2) equipes diferentes podem desenvolver, implantar e evoluir serviços de forma independente, inclusive com stacks tecnológicas diferentes. Desvantagens: (1) maior complexidade operacional (orquestração, monitoramento, comunicação entre serviços via rede, que é menos confiável que chamadas locais); (2) dificuldade em garantir consistência de dados entre serviços que não compartilham banco de dados (exige padrões como Saga, eventual consistency).",
            },

            // ---------------- Gestão Ágil de Projetos ----------------
            new QuestaoMultiplaEscolha
            {
                Assunto = agil,
                Enunciado = "No Scrum, quem é o responsável por priorizar e gerenciar o Product Backlog?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'C',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Scrum Master" },
                    new() { Letra = 'B', Texto = "Time de Desenvolvimento" },
                    new() { Letra = 'C', Texto = "Product Owner" },
                    new() { Letra = 'D', Texto = "Cliente final, diretamente" },
                },
            },
            new QuestaoMultiplaEscolha
            {
                Assunto = agil,
                Enunciado = "No Kanban, qual é o conceito usado para limitar a quantidade de tarefas em andamento simultaneamente, evitando sobrecarga da equipe?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "WIP (Work In Progress) Limit" },
                    new() { Letra = 'B', Texto = "Sprint Backlog" },
                    new() { Letra = 'C', Texto = "Definition of Done" },
                    new() { Letra = 'D', Texto = "Velocity" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = agil,
                Enunciado = "O Manifesto Ágil valoriza \"indivíduos e interações\" mais do que \"processos e ferramentas\", embora reconheça valor em ambos os lados.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = agil,
                Enunciado = "No Scrum, a Sprint Retrospectiva tem como objetivo apresentar o incremento de software pronto para os stakeholders/clientes.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = agil,
                Enunciado = "Associe cada cerimônia/evento do Scrum à sua principal finalidade.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Sprint Planning", Correspondente = "Planejar o que será feito na próxima Sprint" },
                    new() { Ordem = 1, Termo = "Daily Scrum", Correspondente = "Sincronização diária rápida do time" },
                    new() { Ordem = 2, Termo = "Sprint Review", Correspondente = "Apresentar o incremento pronto aos stakeholders" },
                    new() { Ordem = 3, Termo = "Sprint Retrospective", Correspondente = "Refletir sobre o processo e identificar melhorias" },
                },
            },
            new QuestaoRespostaBreve
            {
                Assunto = agil,
                Enunciado = "Como se chama a unidade de medida usada em muitos times ágeis para estimar o esforço/complexidade relativa de um item do backlog (frequentemente usando a sequência de Fibonacci)?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Story Points (pontos de história)",
            },

            // ---------------- DevOps e Integração Contínua ----------------
            new QuestaoMultiplaEscolha
            {
                Assunto = devops,
                Enunciado = "O principal objetivo da cultura DevOps é:",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Separar completamente as equipes de Desenvolvimento e Operações" },
                    new() { Letra = 'B', Texto = "Aproximar Desenvolvimento e Operações, automatizando e agilizando entregas" },
                    new() { Letra = 'C', Texto = "Eliminar totalmente a necessidade de testes automatizados" },
                    new() { Letra = 'D', Texto = "Substituir o papel do Product Owner" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = devops,
                Enunciado = "Integração Contínua (CI) consiste em integrar o código de diferentes desenvolvedores com frequência (várias vezes ao dia), rodando testes automatizados a cada integração.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = devops,
                Enunciado = "Entrega Contínua (Continuous Delivery) garante que o software esteja sempre em um estado pronto para ser implantado em produção, mesmo que a implantação final ainda dependa de uma decisão manual.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = devops,
                Enunciado = "Qual é o nome da prática de definir e gerenciar infraestrutura (servidores, redes) através de arquivos de configuração versionados, em vez de configuração manual?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Infraestrutura como Código (Infrastructure as Code)",
            },
            new QuestaoDiscursiva
            {
                Assunto = devops,
                Enunciado = "Explique a diferença entre Entrega Contínua (Continuous Delivery) e Implantação Contínua (Continuous Deployment).",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Na Entrega Contínua, o software passa por todas as etapas do pipeline (build, testes automatizados) e fica sempre pronto para ir para produção, mas a implantação em produção ainda depende de uma aprovação/gatilho manual. Na Implantação Contínua, esse último passo também é automatizado: toda alteração que passa nos testes é implantada em produção automaticamente, sem intervenção humana.",
            },

            // ---------------- Manutenção e Evolução de Software ----------------
            new QuestaoMultiplaEscolha
            {
                Assunto = manutencao,
                Enunciado = "\"Débito técnico\" (technical debt) é um termo usado para descrever:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "O custo futuro implícito de escolhas de projeto/código de curto prazo em vez de soluções melhores, porém mais demoradas" },
                    new() { Letra = 'B', Texto = "O valor financeiro pago para licenciar um software de terceiros" },
                    new() { Letra = 'C', Texto = "O tempo gasto exclusivamente escrevendo testes automatizados" },
                    new() { Letra = 'D', Texto = "Um tipo de bug que só ocorre em produção" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = manutencao,
                Enunciado = "\"Refatoração\" (refactoring) é o processo de alterar a estrutura interna do código sem mudar seu comportamento externo observável.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = manutencao,
                Enunciado = "Manutenção corretiva é o tipo de manutenção realizada para adaptar o software a novos requisitos do negócio, não relacionada a defeitos.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = manutencao,
                Enunciado = "Associe cada tipo de manutenção de software à sua definição.",
                Dificuldade = Dificuldade.Dificil,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Corretiva", Correspondente = "Corrige defeitos/bugs identificados no software" },
                    new() { Ordem = 1, Termo = "Adaptativa", Correspondente = "Adapta o software a mudanças no ambiente (SO, hardware, regulação)" },
                    new() { Ordem = 2, Termo = "Evolutiva (perfectiva)", Correspondente = "Adiciona novas funcionalidades ou melhora as existentes" },
                    new() { Ordem = 3, Termo = "Preventiva", Correspondente = "Melhora a manutenibilidade futura, antecipando problemas" },
                },
            },
            new QuestaoDiscursiva
            {
                Assunto = manutencao,
                Enunciado = "Explique por que a fase de manutenção costuma representar a maior parte do custo total de um sistema ao longo do seu ciclo de vida.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Porque um sistema de software, uma vez em produção, tende a permanecer em uso por anos (às vezes décadas), período durante o qual precisa continuamente corrigir defeitos (manutenção corretiva), se adaptar a mudanças de ambiente/tecnologia (adaptativa), incorporar novas funcionalidades exigidas pelo negócio (evolutiva) e ser melhorado internamente para continuar manutenível (preventiva) — o esforço acumulado de todas essas atividades, ao longo de todo o tempo de vida útil do sistema, costuma superar em muito o esforço do desenvolvimento inicial, que é uma fase relativamente curta em comparação.",
            },
        };

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
