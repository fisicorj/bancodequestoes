using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Disciplina nova: Programação (padrão igual DbSeederFisica.cs).
public static class DbSeederProgramacao
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Programação");

        var logica = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Lógica de Programação e Estruturas de Controle");
        var estruturasDados = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Estruturas de Dados");
        var algoritmos = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Algoritmos e Complexidade");
        var poo = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Programação Orientada a Objetos");
        var paradigmas = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Paradigmas de Programação");
        var recursao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Recursão");

        var candidatas = new List<Questao>();

        // ---------------- Lógica de Programação e Estruturas de Controle ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = logica,
            Enunciado = "Qual estrutura de controle é usada para tomar decisões, executando um bloco de código apenas se uma condição for verdadeira?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "if / else" },
                new() { Letra = 'B', Texto = "for" },
                new() { Letra = 'C', Texto = "while" },
                new() { Letra = 'D', Texto = "switch (obrigatoriamente)" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = logica,
            Enunciado = "Um laço \"for\" tradicional (com contador) é adequado quando se sabe, de antemão, quantas vezes o bloco de código deve ser repetido.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = logica,
            Enunciado = "Em um laço \"do-while\", o bloco de código é executado pelo menos uma vez, mesmo que a condição seja falsa desde o início.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoLacunas
        {
            Assunto = logica,
            Enunciado = "Os operadores lógicos booleanos mais comuns em linguagens de programação são: ___ (E), ___ (OU) e NOT (negação).",
            Dificuldade = Dificuldade.Facil,
            Lacunas = new()
            {
                new() { Ordem = 0, RespostaEsperada = "AND" },
                new() { Ordem = 1, RespostaEsperada = "OR" },
            },
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = logica,
            Enunciado = "Como é chamada a variável do tipo booleano usada para controlar a repetição (ou não) de um laço, geralmente atualizada dentro dele?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Flag (ou variável de controle)",
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = logica,
            Enunciado = "Explique a diferença entre os laços \"for\", \"while\" e \"do-while\", indicando quando cada um é mais apropriado.",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "\"for\" é indicado quando se conhece de antemão o número de repetições (usa contador). \"while\" testa a condição ANTES de cada execução do bloco — indicado quando não se sabe quantas vezes o laço vai repetir, e é possível que o bloco nunca seja executado (condição falsa já na primeira checagem). \"do-while\" testa a condição DEPOIS de cada execução — garante que o bloco execute pelo menos uma vez, útil por exemplo em menus interativos que devem ser exibidos ao menos uma vez antes de perguntar se o usuário quer continuar.",
        });

        // Trace de laço simples: soma de 1 até N. Resultado = N*(N+1)/2 (soma de Gauss).
        var somaGaussVariantes = new int[] { 5, 10, 6, 8, 20, 4, 15, 7, 12, 9, 25, 3 };
        for (var i = 0; i < somaGaussVariantes.Length; i++)
        {
            var n = somaGaussVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = logica,
                Enunciado = $"Um laço percorre uma variável inteira de 1 até {n} (inclusive), somando o valor da variável a um acumulador iniciado em 0 a cada iteração. Qual é o valor final do acumulador ao término do laço?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = n * (n + 1) / 2,
                Tolerancia = 0m,
            });
        }

        // Número de iterações de um laço "for (i = INICIO; i < FIM; i++)".
        var iteracoesVariantes = new (int Inicio, int Fim)[]
        {
            (0, 10), (0, 5), (1, 8), (0, 20), (2, 12), (0, 100), (5, 15), (0, 7), (1, 11), (3, 13), (0, 50), (10, 25),
        };
        for (var i = 0; i < iteracoesVariantes.Length; i++)
        {
            var (inicio, fim) = iteracoesVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = logica,
                Enunciado = $"Quantas vezes o bloco de código é executado no laço \"for (i = {inicio}; i < {fim}; i++)\" (assumindo incremento de 1 em 1)?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = fim - inicio,
                Tolerancia = 0m,
            });
        }

        // ---------------- Estruturas de Dados ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = estruturasDados,
            Enunciado = "Em uma Pilha (Stack), qual é a política de remoção de elementos?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "FIFO - o primeiro inserido é o primeiro removido" },
                new() { Letra = 'B', Texto = "LIFO - o último inserido é o primeiro removido" },
                new() { Letra = 'C', Texto = "Remoção por prioridade" },
                new() { Letra = 'D', Texto = "Remoção em ordem alfabética" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = estruturasDados,
            Enunciado = "Em uma tabela hash (hash table) bem dimensionada, a busca por uma chave tem complexidade média O(1).",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = estruturasDados,
            Enunciado = "Uma Lista Encadeada permite acesso direto (O(1)) a um elemento qualquer por índice, assim como um vetor (array).",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = estruturasDados,
            Enunciado = "Associe cada estrutura de dados à sua principal aplicação prática.",
            Dificuldade = Dificuldade.Media,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "Pilha", Correspondente = "Controle de chamadas de função (call stack), desfazer ações (undo)" },
                new() { Ordem = 1, Termo = "Fila", Correspondente = "Fila de impressão, fila de atendimento" },
                new() { Ordem = 2, Termo = "Árvore Binária de Busca", Correspondente = "Busca, inserção e remoção ordenadas eficientes" },
                new() { Ordem = 3, Termo = "Tabela Hash", Correspondente = "Busca por chave em tempo médio constante" },
            },
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = estruturasDados,
            Enunciado = "Explique a diferença entre uma Fila (queue) e uma Pilha (stack), incluindo suas políticas de inserção/remoção.",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Fila (queue) segue a política FIFO (First In, First Out): o primeiro elemento inserido é o primeiro a ser removido — como uma fila de pessoas. Pilha (stack) segue a política LIFO (Last In, First Out): o último elemento inserido é o primeiro a ser removido — como uma pilha de pratos, onde só se pode retirar o de cima.",
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = estruturasDados,
            Enunciado = "Qual é o nome da estrutura de dados hierárquica em que cada nó tem, no máximo, dois filhos (chamados filho esquerdo e filho direito)?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Árvore binária",
        });

        // ---------------- Algoritmos e Complexidade ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = algoritmos,
            Enunciado = "Qual é a complexidade de tempo, no pior caso, do algoritmo Quick Sort?",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = 'C',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "O(log n)" },
                new() { Letra = 'B', Texto = "O(n log n)" },
                new() { Letra = 'C', Texto = "O(n²)" },
                new() { Letra = 'D', Texto = "O(n!)" },
            },
        });
        candidatas.Add(new QuestaoMultiplaEscolha
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
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = algoritmos,
            Enunciado = "O algoritmo Merge Sort tem complexidade de tempo O(n log n) tanto no melhor quanto no pior caso.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = algoritmos,
            Enunciado = "A busca binária pode ser aplicada em um vetor não ordenado, com a mesma eficiência de um vetor ordenado.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = algoritmos,
            Enunciado = "Cite o nome de um algoritmo de ordenação simples, porém ineficiente (O(n²)), que compara elementos adjacentes e os troca de posição repetidamente.",
            Dificuldade = Dificuldade.Facil,
            RespostaEsperada = "Bubble Sort",
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = algoritmos,
            Enunciado = "Explique o que é a notação Big-O e por que ela é útil para comparar algoritmos.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "A notação Big-O (O grande) descreve o comportamento assintótico (limite superior) do tempo de execução (ou uso de memória) de um algoritmo em função do tamanho da entrada (n), à medida que n cresce, ignorando constantes e termos de menor ordem. Ela é útil porque permite comparar algoritmos de forma independente de hardware/implementação específica, focando em como o desempenho escala — por exemplo, um algoritmo O(n) sempre será mais eficiente que um O(n²) para entradas suficientemente grandes, mesmo que constantes façam o O(n²) parecer mais rápido para entradas pequenas.",
        });

        // Número de comparações da busca binária no pior caso: ceil(log2(n)).
        var buscaBinariaVariantes = new int[] { 8, 16, 32, 64, 128, 256, 1000, 100, 50, 500, 1024, 10 };
        for (var i = 0; i < buscaBinariaVariantes.Length; i++)
        {
            var n = buscaBinariaVariantes[i];
            var comparacoes = (int)Math.Ceiling(Math.Log2(n));
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = algoritmos,
                Enunciado = $"Em um vetor ordenado com {n} elementos, qual é o número MÁXIMO de comparações necessárias para localizar (ou concluir a ausência de) um elemento usando busca binária (aproxime para o inteiro imediatamente acima, se necessário)?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = comparacoes,
                Tolerancia = 1m,
            });
        }

        // ---------------- Programação Orientada a Objetos ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = poo,
            Enunciado = "Qual pilar da Orientação a Objetos permite que uma classe filha reutilize e estenda o comportamento de uma classe pai?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Encapsulamento" },
                new() { Letra = 'B', Texto = "Herança" },
                new() { Letra = 'C', Texto = "Polimorfismo" },
                new() { Letra = 'D', Texto = "Abstração" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = poo,
            Enunciado = "Polimorfismo permite que objetos de classes diferentes respondam de formas diferentes a uma mesma mensagem (mesmo método/interface).",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = poo,
            Enunciado = "Encapsulamento consiste em expor todos os atributos de uma classe publicamente, para facilitar o acesso direto por outras classes.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = poo,
            Enunciado = "Associe cada pilar da Orientação a Objetos à sua definição.",
            Dificuldade = Dificuldade.Media,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "Encapsulamento", Correspondente = "Esconder detalhes internos, expor só o necessário" },
                new() { Ordem = 1, Termo = "Herança", Correspondente = "Reutilizar/estender comportamento de uma classe pai" },
                new() { Ordem = 2, Termo = "Polimorfismo", Correspondente = "Mesma interface, comportamentos diferentes" },
                new() { Ordem = 3, Termo = "Abstração", Correspondente = "Modelar só os aspectos relevantes de um conceito" },
            },
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = poo,
            Enunciado = "Explique a diferença entre uma classe abstrata e uma interface, do ponto de vista de projeto orientado a objetos.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Uma classe abstrata pode conter tanto métodos com implementação quanto métodos abstratos (sem implementação, que as subclasses devem implementar), além de atributos e estado — serve para compartilhar código comum entre classes relacionadas, e uma classe só pode herdar de UMA classe abstrata (herança simples). Uma interface define apenas um contrato (assinaturas de métodos, geralmente sem implementação, embora algumas linguagens modernas permitam métodos default), sem estado próprio — uma classe pode implementar VÁRIAS interfaces ao mesmo tempo, sendo mais flexível para expressar capacidades combináveis.",
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = poo,
            Enunciado = "Como se chama o método especial, chamado automaticamente quando um novo objeto de uma classe é criado, responsável por inicializar seus atributos?",
            Dificuldade = Dificuldade.Facil,
            RespostaEsperada = "Construtor",
        });

        // ---------------- Paradigmas de Programação ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = paradigmas,
            Enunciado = "No paradigma de programação funcional, funções são tratadas como:",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Cidadãs de primeira classe (podem ser passadas como argumento, retornadas, atribuídas a variáveis)" },
                new() { Letra = 'B', Texto = "Estruturas exclusivamente ligadas a objetos" },
                new() { Letra = 'C', Texto = "Blocos que sempre alteram variáveis globais" },
                new() { Letra = 'D', Texto = "Equivalentes a laços de repetição" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = paradigmas,
            Enunciado = "Uma função pura, no paradigma funcional, sempre produz a mesma saída para a mesma entrada e não causa efeitos colaterais observáveis (como alterar estado externo).",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = paradigmas,
            Enunciado = "O paradigma imperativo descreve o QUE deve ser calculado, sem especificar COMO, ao contrário do paradigma declarativo.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoLacunas
        {
            Assunto = paradigmas,
            Enunciado = "Os principais paradigmas de programação incluem o ___ (baseado em objetos com estado e comportamento), o ___ (baseado em funções matemáticas e imutabilidade) e o imperativo/procedural (baseado em comandos sequenciais que alteram estado).",
            Dificuldade = Dificuldade.Media,
            Lacunas = new()
            {
                new() { Ordem = 0, RespostaEsperada = "orientado a objetos" },
                new() { Ordem = 1, RespostaEsperada = "funcional" },
            },
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = paradigmas,
            Enunciado = "Cite uma vantagem do paradigma funcional em relação ao imperativo no contexto de programação concorrente/paralela.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "No paradigma funcional, o uso extensivo de funções puras e estruturas de dados imutáveis reduz (ou elimina) o compartilhamento de estado mutável entre diferentes threads/processos — como não há efeitos colaterais alterando variáveis compartilhadas, o risco de condições de corrida (race conditions) e a necessidade de mecanismos de sincronização (locks, semáforos) diminuem bastante, tornando o código mais fácil de paralelizar com segurança.",
        });

        // ---------------- Recursão ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = recursao,
            Enunciado = "Toda função recursiva bem construída precisa ter, obrigatoriamente:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Um caso base (condição de parada)" },
                new() { Letra = 'B', Texto = "Pelo menos dois parâmetros" },
                new() { Letra = 'C', Texto = "Um laço \"for\" interno" },
                new() { Letra = 'D', Texto = "Uma variável global" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = recursao,
            Enunciado = "Uma função recursiva sem caso base (ou com caso base inalcançável) tende a causar um estouro de pilha (stack overflow) em tempo de execução.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = recursao,
            Enunciado = "Todo problema resolvido com recursão pode, em princípio, também ser resolvido de forma iterativa (com laços), embora às vezes com código mais complexo.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = recursao,
            Enunciado = "Explique o conceito de recursão e cite um exemplo clássico de problema resolvido recursivamente.",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Recursão é uma técnica em que uma função resolve um problema chamando a si mesma para resolver uma versão menor (ou mais simples) do mesmo problema, até atingir um caso base que pode ser resolvido diretamente, sem novas chamadas. Exemplo clássico: cálculo do fatorial de n, onde fatorial(n) = n × fatorial(n-1), com caso base fatorial(0) = 1. Outros exemplos comuns: sequência de Fibonacci, busca binária, percursos em árvores e o algoritmo Merge Sort.",
        });

        // Fatorial (n!), calculado em tempo de execução.
        var fatorialVariantes = new int[] { 3, 4, 5, 6, 7, 2, 8, 1, 9, 5, 6, 4 };
        for (var i = 0; i < fatorialVariantes.Length; i++)
        {
            var n = fatorialVariantes[i];
            long fatorial = 1;
            for (var k = 2; k <= n; k++)
            {
                fatorial *= k;
            }

            candidatas.Add(new QuestaoNumerica
            {
                Assunto = recursao,
                Enunciado = $"Considere a função recursiva fatorial(n) = n × fatorial(n-1), com fatorial(0) = 1. Qual é o valor de fatorial({n})?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = fatorial,
                Tolerancia = 0m,
            });
        }

        // Fibonacci (F0=0, F1=1, F(n)=F(n-1)+F(n-2)), calculado em tempo de execução.
        var fibonacciIndices = new int[] { 5, 6, 7, 8, 9, 10, 4, 11, 12, 3, 6, 8 };
        for (var i = 0; i < fibonacciIndices.Length; i++)
        {
            var n = fibonacciIndices[i];
            long a = 0;
            long b = 1;
            for (var k = 0; k < n; k++)
            {
                (a, b) = (b, a + b);
            }

            candidatas.Add(new QuestaoNumerica
            {
                Assunto = recursao,
                Enunciado = $"Na sequência de Fibonacci (F(0) = 0, F(1) = 1, F(n) = F(n-1) + F(n-2)), qual é o valor de F({n})?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = a,
                Tolerancia = 0m,
            });
        }

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
