using BancoQuestoes.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoQuestoes.Data;

// Popula questões de teste reaproveitando Disciplina/Assuntos existentes e só
// inserindo enunciados novos — seguro rodar de novo, nunca duplica.
public static class DbSeeder
{
    // Conserta questões de seeder antigo sem CriadoPorId (ficavam invisíveis);
    // não mexe em questão real, pois nenhuma da UI tem CriadoPorId nulo.
    public static async Task RepararQuestoesSemDonoAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var orfas = await db.Questoes.Where(q => q.CriadoPorId == null).ToListAsync();
        if (orfas.Count == 0)
        {
            return;
        }

        foreach (var q in orfas)
        {
            q.CriadoPorId = criadoPorId;
            q.Visibilidade = VisibilidadeQuestao.Compartilhada;
        }

        await db.SaveChangesAsync();
    }

    public static async Task SeedEngenhariaDeSoftwareAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await ObterOuCriarDisciplinaAsync(db, "Engenharia de Software");

        var processos = ObterOuCriarAssunto(db, disciplina, "Processos de Software");
        var requisitos = ObterOuCriarAssunto(db, disciplina, "Engenharia de Requisitos");
        var uml = ObterOuCriarAssunto(db, disciplina, "Modelagem UML");
        var testes = ObterOuCriarAssunto(db, disciplina, "Testes de Software");
        var padroes = ObterOuCriarAssunto(db, disciplina, "Padrões de Projeto");
        var qualidade = ObterOuCriarAssunto(db, disciplina, "Qualidade de Software");

        var candidatas = new List<Questao>
        {
            // --- Processos de Software ---
            new QuestaoMultiplaEscolha
            {
                Assunto = processos,
                Enunciado = "No modelo Scrum, qual é a duração recomendada de uma Sprint?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "De 1 a 4 semanas" },
                    new() { Letra = 'B', Texto = "6 meses" },
                    new() { Letra = 'C', Texto = "1 dia" },
                    new() { Letra = 'D', Texto = "1 ano" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = processos,
                Enunciado = "O modelo em cascata (Waterfall) permite retornar facilmente a fases anteriores do desenvolvimento sem custo adicional.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = processos,
                Enunciado = "Explique a diferença entre metodologias ágeis e o modelo em cascata quanto à forma de lidar com mudanças de requisitos.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Metodologias ágeis (como Scrum e XP) tratam mudanças de requisitos como algo esperado, incorporando-as em ciclos curtos e iterativos (sprints). O modelo em cascata trata os requisitos como fixos desde o início, com fases sequenciais (levantamento, projeto, implementação, teste, manutenção), tornando mudanças tardias custosas e difíceis de acomodar.",
            },
            new QuestaoLacunas
            {
                Assunto = processos,
                Enunciado = "O ciclo de melhoria contínua PDCA é composto por quatro etapas: Planejar, ___, Verificar e ___.",
                Dificuldade = Dificuldade.Media,
                Lacunas = new()
                {
                    new() { Ordem = 0, RespostaEsperada = "Executar" },
                    new() { Ordem = 1, RespostaEsperada = "Agir" },
                },
            },

            // --- Engenharia de Requisitos ---
            new QuestaoMultiplaEscolha
            {
                Assunto = requisitos,
                Enunciado = "Qual das opções abaixo é um exemplo de requisito não funcional?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "O sistema deve permitir login com email e senha" },
                    new() { Letra = 'B', Texto = "O sistema deve responder em até 2 segundos" },
                    new() { Letra = 'C', Texto = "O sistema deve gerar um relatório em PDF" },
                    new() { Letra = 'D', Texto = "O sistema deve permitir cadastro de usuários" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = requisitos,
                Enunciado = "Requisitos funcionais descrevem o que o sistema deve fazer, enquanto requisitos não funcionais descrevem restrições sobre como o sistema deve se comportar (desempenho, segurança, usabilidade etc.).",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoDiscursiva
            {
                Assunto = requisitos,
                Enunciado = "Cite e explique brevemente duas técnicas de elicitação de requisitos.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Exemplos: entrevistas (conversas estruturadas com stakeholders para levantar necessidades), questionários (coleta padronizada de um público maior), observação (acompanhar o usuário em seu ambiente real de trabalho), workshops/JAD (sessões colaborativas com múltiplos stakeholders) e prototipação (construir um protótipo para validar o entendimento).",
            },

            // --- Modelagem UML ---
            new QuestaoMultiplaEscolha
            {
                Assunto = uml,
                Enunciado = "Qual diagrama UML representa a interação entre objetos ao longo do tempo, mostrando a troca de mensagens?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Diagrama de Classes" },
                    new() { Letra = 'B', Texto = "Diagrama de Sequência" },
                    new() { Letra = 'C', Texto = "Diagrama de Casos de Uso" },
                    new() { Letra = 'D', Texto = "Diagrama de Componentes" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = uml,
                Enunciado = "O Diagrama de Casos de Uso tem como principal objetivo detalhar a estrutura interna das classes do sistema.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = uml,
                Enunciado = "O que representa uma associação de agregação em um diagrama de classes UML, e como ela se diferencia de uma composição?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Agregação representa uma relação 'todo-parte' fraca, em que a parte pode existir independentemente do todo (ex: um Departamento tem Funcionários, mas o Funcionário continua existindo se o Departamento for extinto). Composição é uma relação 'todo-parte' forte, em que a parte não existe sem o todo (ex: uma Casa tem Cômodos; se a Casa é destruída, os Cômodos deixam de existir). Composição é representada por um losango preenchido; agregação, por um losango vazio.",
            },

            // --- Testes de Software ---
            new QuestaoMultiplaEscolha
            {
                Assunto = testes,
                Enunciado = "Qual tipo de teste verifica se módulos individuais do sistema funcionam corretamente de forma isolada?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Teste de unidade" },
                    new() { Letra = 'B', Texto = "Teste de integração" },
                    new() { Letra = 'C', Texto = "Teste de sistema" },
                    new() { Letra = 'D', Texto = "Teste de aceitação" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = testes,
                Enunciado = "Teste de caixa-preta (black-box) avalia o código-fonte internamente, verificando a cobertura de cada linha executada.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = testes,
                Enunciado = "Explique o conceito de Test-Driven Development (TDD) e descreva seu ciclo básico.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "TDD é uma prática em que os testes são escritos antes do código de produção. Segue o ciclo Red-Green-Refactor: (1) Red - escreve-se um teste que falha, pois a funcionalidade ainda não existe; (2) Green - escreve-se o código mínimo necessário para o teste passar; (3) Refactor - refatora-se o código mantendo os testes passando, melhorando a qualidade sem alterar o comportamento.",
            },
            new QuestaoNumerica
            {
                Assunto = testes,
                Enunciado = "Se uma suíte de testes cobre 180 das 240 linhas de código de um módulo, qual é a porcentagem de cobertura de testes (em %)?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = 75m,
                Tolerancia = 0m,
            },

            // --- Padrões de Projeto ---
            new QuestaoMultiplaEscolha
            {
                Assunto = padroes,
                Enunciado = "Qual padrão de projeto garante que uma classe tenha apenas uma instância e fornece um ponto de acesso global a ela?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Factory Method" },
                    new() { Letra = 'B', Texto = "Singleton" },
                    new() { Letra = 'C', Texto = "Observer" },
                    new() { Letra = 'D', Texto = "Decorator" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = padroes,
                Enunciado = "O padrão Observer permite que um objeto notifique automaticamente outros objetos interessados quando seu estado muda.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoAssociacao
            {
                Assunto = padroes,
                Enunciado = "Associe cada padrão de projeto (GoF) à sua categoria.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Singleton", Correspondente = "Padrão Criacional" },
                    new() { Ordem = 1, Termo = "Adapter", Correspondente = "Padrão Estrutural" },
                    new() { Ordem = 2, Termo = "Observer", Correspondente = "Padrão Comportamental" },
                    new() { Ordem = 3, Termo = "Factory Method", Correspondente = "Padrão Criacional" },
                },
            },

            // --- Qualidade de Software ---
            new QuestaoMultiplaEscolha
            {
                Assunto = qualidade,
                Enunciado = "Qual das opções é uma prática de Integração Contínua (CI)?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Integrar o código apenas no final do projeto" },
                    new() { Letra = 'B', Texto = "Integrar o código com frequência, rodando testes automatizados a cada integração" },
                    new() { Letra = 'C', Texto = "Evitar testes automatizados para acelerar entregas" },
                    new() { Letra = 'D', Texto = "Compilar o sistema manualmente uma vez por mês" },
                },
            },
            new QuestaoDiscursiva
            {
                Assunto = qualidade,
                Enunciado = "Cite três atributos de qualidade de software e explique brevemente cada um.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Exemplos: Funcionalidade (o sistema atende às necessidades declaradas), Confiabilidade (mantém o desempenho sob condições especificadas), Usabilidade (facilidade de uso e aprendizado), Eficiência (uso adequado de recursos), Manutenibilidade (facilidade de modificar o sistema) e Portabilidade (facilidade de transferir o sistema para outros ambientes).",
                CriterioAvaliacao = "Aceitar qualquer três atributos da ISO/IEC 25010, desde que explicados corretamente.",
            },
            new QuestaoRespostaBreve
            {
                Assunto = qualidade,
                Enunciado = "Qual é a norma internacional (ISO/IEC) que define o modelo de qualidade de produto de software?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "ISO/IEC 25010",
            },
        };

        await AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    public static async Task SeedMatematicaAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await ObterOuCriarDisciplinaAsync(db, "Matemática");

        var algebra = ObterOuCriarAssunto(db, disciplina, "Álgebra");
        var geometria = ObterOuCriarAssunto(db, disciplina, "Geometria");
        var trigonometria = ObterOuCriarAssunto(db, disciplina, "Trigonometria");
        var calculo = ObterOuCriarAssunto(db, disciplina, "Cálculo Diferencial e Integral");
        var estatistica = ObterOuCriarAssunto(db, disciplina, "Estatística e Probabilidade");
        var funcoes = ObterOuCriarAssunto(db, disciplina, "Funções");

        var candidatas = new List<Questao>
        {
            // --- Álgebra ---
            new QuestaoMultiplaEscolha
            {
                Assunto = algebra,
                Enunciado = "Qual é a solução da equação x² - 5x + 6 = 0?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "x = 2 ou x = 3" },
                    new() { Letra = 'B', Texto = "x = 1 ou x = 6" },
                    new() { Letra = 'C', Texto = "x = -2 ou x = -3" },
                    new() { Letra = 'D', Texto = "x = 5 ou x = 6" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = algebra,
                Enunciado = "Toda equação do segundo grau possui necessariamente duas raízes reais distintas.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoNumerica
            {
                Assunto = algebra,
                Enunciado = "Qual o valor de x na equação 2x + 6 = 14?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = 4m,
                Tolerancia = 0m,
            },
            new QuestaoDiscursiva
            {
                Assunto = algebra,
                Enunciado = "Enuncie a fórmula de Bhaskara e explique o papel do discriminante (Δ) na quantidade de raízes reais de uma equação do segundo grau.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "x = (-b ± √Δ)/2a, onde Δ = b² - 4ac. Se Δ > 0, a equação tem duas raízes reais distintas; se Δ = 0, tem uma raiz real (dupla); se Δ < 0, não tem raízes reais (as raízes são complexas).",
            },

            // --- Geometria ---
            new QuestaoMultiplaEscolha
            {
                Assunto = geometria,
                Enunciado = "Qual é a fórmula da área de um triângulo em função da base e da altura?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "(base × altura) / 2" },
                    new() { Letra = 'B', Texto = "base × altura" },
                    new() { Letra = 'C', Texto = "(base + altura) / 2" },
                    new() { Letra = 'D', Texto = "base²" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = geometria,
                Enunciado = "O Teorema de Pitágoras pode ser aplicado a qualquer triângulo, independentemente de seus ângulos.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoNumerica
            {
                Assunto = geometria,
                Enunciado = "Calcule a área de um círculo de raio 5 cm (use π ≈ 3,14). Informe o resultado em cm².",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = 78.5m,
                Tolerancia = 0.5m,
            },

            // --- Trigonometria ---
            new QuestaoMultiplaEscolha
            {
                Assunto = trigonometria,
                Enunciado = "Qual é o valor de sen(30°)?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "0,5" },
                    new() { Letra = 'B', Texto = "1" },
                    new() { Letra = 'C', Texto = "0" },
                    new() { Letra = 'D', Texto = "√3 / 2" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = trigonometria,
                Enunciado = "A identidade fundamental da trigonometria afirma que sen²(x) + cos²(x) = 1 para qualquer ângulo x.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = trigonometria,
                Enunciado = "Qual o valor de tan(45°)?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "1",
            },

            // --- Cálculo Diferencial e Integral ---
            new QuestaoMultiplaEscolha
            {
                Assunto = calculo,
                Enunciado = "Qual é a derivada de f(x) = x²?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "f'(x) = 2x" },
                    new() { Letra = 'B', Texto = "f'(x) = x" },
                    new() { Letra = 'C', Texto = "f'(x) = 2" },
                    new() { Letra = 'D', Texto = "f'(x) = x²" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = calculo,
                Enunciado = "A integral definida de uma função sempre representa uma área positiva, mesmo quando a função assume valores negativos no intervalo.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = false,
            },
            new QuestaoNumerica
            {
                Assunto = calculo,
                Enunciado = "Calcule a derivada de f(x) = x³ no ponto x = 2.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = 12m,
                Tolerancia = 0m,
            },
            new QuestaoDiscursiva
            {
                Assunto = calculo,
                Enunciado = "Explique, em linhas gerais, o que estabelece o Teorema Fundamental do Cálculo.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "O Teorema Fundamental do Cálculo estabelece a relação entre derivação e integração: a integral definida de uma função contínua f em um intervalo [a,b] pode ser calculada por meio de uma antiderivada F de f, sendo igual a F(b) - F(a). Ele mostra que a integração é, em certo sentido, a operação inversa da derivação.",
            },

            // --- Estatística e Probabilidade ---
            new QuestaoMultiplaEscolha
            {
                Assunto = estatistica,
                Enunciado = "Qual medida estatística representa a soma de todos os valores dividida pela quantidade de valores?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Média aritmética" },
                    new() { Letra = 'B', Texto = "Mediana" },
                    new() { Letra = 'C', Texto = "Moda" },
                    new() { Letra = 'D', Texto = "Desvio padrão" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = estatistica,
                Enunciado = "A mediana de um conjunto de dados é sempre igual à média aritmética desse conjunto.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoNumerica
            {
                Assunto = estatistica,
                Enunciado = "Calcule a média aritmética dos valores 4, 8, 6, 10 e 2.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = 6m,
                Tolerancia = 0m,
            },

            // --- Funções ---
            new QuestaoMultiplaEscolha
            {
                Assunto = funcoes,
                Enunciado = "O que representa o domínio de uma função?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "O conjunto de valores de entrada (x) para os quais a função está definida" },
                    new() { Letra = 'B', Texto = "O conjunto de valores de saída (y) da função" },
                    new() { Letra = 'C', Texto = "O ponto onde a função cruza o eixo y" },
                    new() { Letra = 'D', Texto = "A inclinação da reta" },
                },
            },
            new QuestaoAssociacao
            {
                Assunto = funcoes,
                Enunciado = "Associe cada tipo de função à sua principal característica.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Função Linear", Correspondente = "y = ax (gráfico é uma reta que passa pela origem)" },
                    new() { Ordem = 1, Termo = "Função Quadrática", Correspondente = "y = ax² + bx + c (gráfico é uma parábola)" },
                    new() { Ordem = 2, Termo = "Função Exponencial", Correspondente = "y = a^x (crescimento ou decaimento exponencial)" },
                    new() { Ordem = 3, Termo = "Função Constante", Correspondente = "y = k (gráfico é uma reta horizontal)" },
                },
            },
            new QuestaoLacunas
            {
                Assunto = funcoes,
                Enunciado = "Na função afim y = ax + b, o coeficiente ___ é chamado de coeficiente angular, e o coeficiente ___ é chamado de coeficiente linear.",
                Dificuldade = Dificuldade.Media,
                Lacunas = new()
                {
                    new() { Ordem = 0, RespostaEsperada = "a" },
                    new() { Ordem = 1, RespostaEsperada = "b" },
                },
            },
        };

        await AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    public static async Task SeedIntroducaoComputacaoAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await ObterOuCriarDisciplinaAsync(db, "Introdução à Computação");

        var logica = ObterOuCriarAssunto(db, disciplina, "Lógica de Programação");
        var estruturas = ObterOuCriarAssunto(db, disciplina, "Estruturas de Dados");
        var algoritmos = ObterOuCriarAssunto(db, disciplina, "Algoritmos e Complexidade");
        var numeracao = ObterOuCriarAssunto(db, disciplina, "Sistemas de Numeração");
        var arquitetura = ObterOuCriarAssunto(db, disciplina, "Arquitetura de Computadores");
        var redes = ObterOuCriarAssunto(db, disciplina, "Redes e Internet");

        var candidatas = new List<Questao>
        {
            // --- Lógica de Programação ---
            new QuestaoMultiplaEscolha
            {
                Assunto = logica,
                Enunciado = "Qual estrutura de repetição executa um bloco de comandos enquanto uma condição for verdadeira, testando a condição antes de cada execução?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "while" },
                    new() { Letra = 'B', Texto = "do-while" },
                    new() { Letra = 'C', Texto = "if" },
                    new() { Letra = 'D', Texto = "switch" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = logica,
                Enunciado = "Em linguagens de programação fortemente tipadas, uma variável deve ter seu tipo declarado (ou inferido) antes de ser utilizada.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = logica,
                Enunciado = "Em programação, o que o operador % (módulo) retorna ao ser aplicado entre dois números inteiros?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "O resto da divisão entre os dois números",
            },
            new QuestaoLacunas
            {
                Assunto = logica,
                Enunciado = "A estrutura de repetição que verifica a condição antes de executar o bloco é chamada de laço ___, enquanto a que executa o bloco pelo menos uma vez antes de verificar a condição é chamada de laço ___.",
                Dificuldade = Dificuldade.Media,
                Lacunas = new()
                {
                    new() { Ordem = 0, RespostaEsperada = "while (enquanto)" },
                    new() { Ordem = 1, RespostaEsperada = "do-while (faça-enquanto)" },
                },
            },

            // --- Estruturas de Dados ---
            new QuestaoMultiplaEscolha
            {
                Assunto = estruturas,
                Enunciado = "Em uma estrutura do tipo Fila (queue), qual é a política de remoção de elementos?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "FIFO - o primeiro elemento inserido é o primeiro removido" },
                    new() { Letra = 'B', Texto = "LIFO - o último inserido é o primeiro removido" },
                    new() { Letra = 'C', Texto = "Remoção em ordem aleatória" },
                    new() { Letra = 'D', Texto = "Remoção por prioridade numérica" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = estruturas,
                Enunciado = "Uma Fila (queue) segue a política LIFO (Last In, First Out).",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = estruturas,
                Enunciado = "Associe cada estrutura de dados à sua principal característica.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Pilha", Correspondente = "Estrutura LIFO (último a entrar, primeiro a sair)" },
                    new() { Ordem = 1, Termo = "Fila", Correspondente = "Estrutura FIFO (primeiro a entrar, primeiro a sair)" },
                    new() { Ordem = 2, Termo = "Lista Encadeada", Correspondente = "Conjunto de nós ligados por ponteiros/referências" },
                    new() { Ordem = 3, Termo = "Árvore Binária", Correspondente = "Estrutura hierárquica em que cada nó tem no máximo dois filhos" },
                },
            },
            new QuestaoDiscursiva
            {
                Assunto = estruturas,
                Enunciado = "Explique a principal diferença entre um vetor (array) e uma lista encadeada quanto à forma de armazenamento e acesso aos elementos.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Um vetor armazena seus elementos em posições contíguas de memória, permitindo acesso direto (O(1)) a qualquer elemento por índice, mas com tamanho fixo ou custoso de redimensionar. Uma lista encadeada armazena elementos em nós espalhados na memória, ligados por ponteiros/referências; o acesso a um elemento específico exige percorrer a lista a partir do início (O(n)), mas a inserção/remoção de elementos é mais flexível, sem precisar realocar toda a estrutura.",
            },

            // --- Algoritmos e Complexidade ---
            new QuestaoMultiplaEscolha
            {
                Assunto = algoritmos,
                Enunciado = "Qual é a complexidade de tempo, no pior caso, do algoritmo de busca binária em um vetor ordenado de n elementos?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "O(log n)" },
                    new() { Letra = 'B', Texto = "O(n)" },
                    new() { Letra = 'C', Texto = "O(n²)" },
                    new() { Letra = 'D', Texto = "O(1)" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = algoritmos,
                Enunciado = "O algoritmo Bubble Sort tem complexidade de tempo O(n log n) no pior caso.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = false,
            },
            new QuestaoRespostaBreve
            {
                Assunto = algoritmos,
                Enunciado = "Cite o nome de um algoritmo de ordenação com complexidade O(n log n) no caso médio.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Merge Sort (ou Quick Sort, Heap Sort)",
            },

            // --- Sistemas de Numeração ---
            new QuestaoMultiplaEscolha
            {
                Assunto = numeracao,
                Enunciado = "Quantos símbolos distintos são usados no sistema de numeração hexadecimal?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "16" },
                    new() { Letra = 'B', Texto = "10" },
                    new() { Letra = 'C', Texto = "8" },
                    new() { Letra = 'D', Texto = "2" },
                },
            },
            new QuestaoNumerica
            {
                Assunto = numeracao,
                Enunciado = "Converta o número binário 1010 para decimal.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = 10m,
                Tolerancia = 0m,
            },
            new QuestaoCertoErrado
            {
                Assunto = numeracao,
                Enunciado = "O sistema binário utiliza apenas os dígitos 0 e 1 para representar qualquer número.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },

            // --- Arquitetura de Computadores ---
            new QuestaoMultiplaEscolha
            {
                Assunto = arquitetura,
                Enunciado = "Qual componente do computador é responsável por executar as instruções e realizar os cálculos do sistema?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "CPU (processador)" },
                    new() { Letra = 'B', Texto = "HD/SSD" },
                    new() { Letra = 'C', Texto = "Placa de rede" },
                    new() { Letra = 'D', Texto = "Monitor" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = arquitetura,
                Enunciado = "A memória RAM é um tipo de memória não volátil, ou seja, mantém os dados armazenados mesmo sem energia elétrica.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = arquitetura,
                Enunciado = "Explique a principal diferença entre a memória RAM e um dispositivo de armazenamento secundário, como um SSD.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "A RAM é uma memória volátil e de acesso rápido, usada para armazenar temporariamente os dados e programas em execução; seu conteúdo é perdido quando o computador é desligado. O SSD (assim como o HD) é um dispositivo de armazenamento secundário, não volátil, mais lento que a RAM, usado para guardar dados e programas de forma permanente, mesmo sem energia.",
            },

            // --- Redes e Internet ---
            new QuestaoMultiplaEscolha
            {
                Assunto = redes,
                Enunciado = "Qual protocolo é utilizado para a transferência de páginas na World Wide Web?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "HTTP" },
                    new() { Letra = 'B', Texto = "FTP" },
                    new() { Letra = 'C', Texto = "SMTP" },
                    new() { Letra = 'D', Texto = "DNS" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = redes,
                Enunciado = "O protocolo TCP garante a entrega ordenada e confiável dos pacotes de dados entre origem e destino.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = redes,
                Enunciado = "Qual é a sigla do protocolo utilizado para o envio de emails?",
                Dificuldade = Dificuldade.Facil,
                RespostaEsperada = "SMTP",
            },
        };

        await AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    // Dependência estrutural da feature ENADE: reaproveita por Codigo, depois
    // por Nome (com backfill), só então cria nova; garante um Assunto genérico.
    public static async Task SeedDisciplinaFormacaoGeralAsync(ApplicationDbContext db)
    {
        var disciplina = await db.Disciplinas
            .Include(d => d.Assuntos)
            .FirstOrDefaultAsync(d => d.Codigo == Disciplina.CodigoFormacaoGeral);

        if (disciplina is null)
        {
            disciplina = await db.Disciplinas
                .Include(d => d.Assuntos)
                .FirstOrDefaultAsync(d => d.Nome == "Formação Geral");

            if (disciplina is null)
            {
                disciplina = new Disciplina { Nome = "Formação Geral", Codigo = Disciplina.CodigoFormacaoGeral };
                db.Disciplinas.Add(disciplina);
            }
            else
            {
                disciplina.Codigo = Disciplina.CodigoFormacaoGeral;
            }
        }

        ObterOuCriarAssunto(db, disciplina, "Conhecimentos Gerais");

        await db.SaveChangesAsync();
    }

    // internal: os seeders de disciplinas novas reaproveitam esses três
    // helpers em vez de duplicar a lógica de "existe? reaproveita : cria".
    internal static async Task<Disciplina> ObterOuCriarDisciplinaAsync(ApplicationDbContext db, string nome)
    {
        var disciplina = await db.Disciplinas
            .Include(d => d.Assuntos)
            .FirstOrDefaultAsync(d => d.Nome == nome);

        if (disciplina is null)
        {
            disciplina = new Disciplina { Nome = nome };
            db.Disciplinas.Add(disciplina);
        }

        return disciplina;
    }

    internal static Assunto ObterOuCriarAssunto(ApplicationDbContext db, Disciplina disciplina, string nome)
    {
        var existente = disciplina.Assuntos.FirstOrDefault(a => a.Nome == nome);
        if (existente is not null)
        {
            return existente;
        }

        var novo = new Assunto { Nome = nome, Disciplina = disciplina };
        disciplina.Assuntos.Add(novo);
        db.Assuntos.Add(novo);
        return novo;
    }

    // Define TipoQuestao a partir do tipo concreto e marca Compartilhada com
    // dono, senão o filtro VisivelPara esconderia de todo mundo; só insere o que não existe.
    internal static async Task AplicarTipoEInserirNovasAsync(ApplicationDbContext db, List<Questao> candidatas, string? criadoPorId)
    {
        foreach (var questao in candidatas)
        {
            questao.TipoQuestao = questao switch
            {
                QuestaoMultiplaEscolha => TipoQuestao.MultiplaEscolha,
                QuestaoDiscursiva => TipoQuestao.Discursiva,
                QuestaoCertoErrado => TipoQuestao.CertoErrado,
                QuestaoAssociacao => TipoQuestao.Associacao,
                QuestaoRespostaBreve => TipoQuestao.RespostaBreve,
                QuestaoNumerica => TipoQuestao.Numerica,
                QuestaoLacunas => TipoQuestao.Lacunas,
                _ => questao.TipoQuestao,
            };
            questao.Visibilidade = VisibilidadeQuestao.Compartilhada;
            questao.CriadoPorId = criadoPorId;
        }

        var enunciadosExistentes = (await db.Questoes.Select(q => q.Enunciado).ToListAsync()).ToHashSet();
        var novas = candidatas.Where(q => !enunciadosExistentes.Contains(q.Enunciado)).ToList();

        if (novas.Count > 0)
        {
            db.Questoes.AddRange(novas);
            await db.SaveChangesAsync();
        }
    }
}
