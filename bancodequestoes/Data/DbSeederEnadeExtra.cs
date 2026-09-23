using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Disciplinas do núcleo profissionalizante ENADE (Banco de Dados, Sistemas
// Operacionais, Arquitetura, Segurança); vínculo com Itens de Matriz é à parte, em DbSeederEnadeVinculo.cs.
public static class DbSeederEnadeExtra
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        await SeedBancoDeDadosAsync(db, criadoPorId);
        await SeedSistemasOperacionaisAsync(db, criadoPorId);
        await SeedArquiteturaDeComputadoresAsync(db, criadoPorId);
        await SeedSegurancaDaInformacaoAsync(db, criadoPorId);
    }

    private static async Task SeedBancoDeDadosAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Banco de Dados");

        var modelagem = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Modelagem de Dados (ER e Relacional)");
        var sql = DbSeeder.ObterOuCriarAssunto(db, disciplina, "SQL e Álgebra Relacional");
        var normalizacao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Normalização");
        var transacoes = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Transações e Concorrência (ACID)");

        var candidatas = new List<Questao>
        {
            new QuestaoMultiplaEscolha
            {
                Assunto = modelagem,
                Enunciado = "No Modelo Entidade-Relacionamento (MER), uma \"entidade fraca\" é aquela que:",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Não possui atributos suficientes para formar uma chave primária própria, dependendo de outra entidade" },
                    new() { Letra = 'B', Texto = "Possui poucos atributos" },
                    new() { Letra = 'C', Texto = "Nunca participa de relacionamentos" },
                    new() { Letra = 'D', Texto = "Só pode existir em bancos NoSQL" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = modelagem,
                Enunciado = "Uma chave estrangeira (foreign key) em uma tabela relacional referencia a chave primária de outra tabela (ou da mesma tabela), garantindo integridade referencial.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = modelagem,
                Enunciado = "Um relacionamento \"muitos para muitos\" (N:N) entre duas entidades é implementado diretamente, no modelo relacional, sem a necessidade de uma tabela associativa.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = modelagem,
                Enunciado = "Explique como um relacionamento N:N (muitos para muitos) entre duas entidades é representado no modelo relacional.",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Um relacionamento N:N é representado através de uma tabela associativa (ou tabela de junção), que possui, no mínimo, duas chaves estrangeiras — uma referenciando a chave primária de cada uma das entidades envolvidas. Essa tabela associativa transforma o relacionamento N:N original em dois relacionamentos 1:N (um entre cada entidade original e a tabela associativa).",
            },
            new QuestaoRespostaBreve
            {
                Assunto = modelagem,
                Enunciado = "Qual é o nome da técnica gráfica clássica usada para representar entidades, atributos e relacionamentos na modelagem conceitual de um banco de dados?",
                Dificuldade = Dificuldade.Facil,
                RespostaEsperada = "Diagrama Entidade-Relacionamento (DER)",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = sql,
                Enunciado = "Qual comando SQL é usado para recuperar (consultar) dados de uma ou mais tabelas?",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "SELECT" },
                    new() { Letra = 'B', Texto = "INSERT" },
                    new() { Letra = 'C', Texto = "UPDATE" },
                    new() { Letra = 'D', Texto = "DELETE" },
                },
            },
            new QuestaoMultiplaEscolha
            {
                Assunto = sql,
                Enunciado = "Qual tipo de JOIN retorna TODAS as linhas da tabela da esquerda, mesmo quando não há correspondência na tabela da direita (preenchendo com NULL nesse caso)?",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "INNER JOIN" },
                    new() { Letra = 'B', Texto = "LEFT JOIN" },
                    new() { Letra = 'C', Texto = "CROSS JOIN" },
                    new() { Letra = 'D', Texto = "SELF JOIN (obrigatoriamente)" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = sql,
                Enunciado = "A cláusula WHERE é usada para filtrar linhas ANTES de qualquer agrupamento (GROUP BY), enquanto a cláusula HAVING filtra grupos DEPOIS do agrupamento.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = sql,
                Enunciado = "Um INNER JOIN retorna apenas as linhas que possuem correspondência em AMBAS as tabelas envolvidas na junção.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = sql,
                Enunciado = "Qual comando SQL (DDL) é usado para criar uma nova tabela em um banco de dados relacional?",
                Dificuldade = Dificuldade.Facil,
                RespostaEsperada = "CREATE TABLE",
            },
            new QuestaoLacunas
            {
                Assunto = sql,
                Enunciado = "As categorias de comandos SQL incluem DDL (Data Definition Language, ex.: CREATE, ALTER), DML (Data ___ Language, ex.: SELECT, INSERT, UPDATE, DELETE) e DCL (Data ___ Language, ex.: GRANT, REVOKE).",
                Dificuldade = Dificuldade.Dificil,
                Lacunas = new()
                {
                    new() { Ordem = 0, RespostaEsperada = "Manipulation" },
                    new() { Ordem = 1, RespostaEsperada = "Control" },
                },
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = normalizacao,
                Enunciado = "Uma tabela está na Primeira Forma Normal (1FN) quando:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Todos os seus atributos são atômicos (indivisíveis) e não há grupos repetitivos" },
                    new() { Letra = 'B', Texto = "Ela não possui chave primária" },
                    new() { Letra = 'C', Texto = "Todos os seus atributos dependem só de parte da chave primária" },
                    new() { Letra = 'D', Texto = "Ela possui apenas dois atributos" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = normalizacao,
                Enunciado = "O principal objetivo da normalização é reduzir redundância de dados e evitar anomalias de inserção, atualização e remoção.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = normalizacao,
                Enunciado = "Uma tabela na Terceira Forma Normal (3FN) pode ter atributos não-chave que dependem de outros atributos não-chave (dependência transitiva).",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = normalizacao,
                Enunciado = "Explique o que é uma dependência funcional parcial e por que ela viola a Segunda Forma Normal (2FN).",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Uma dependência funcional parcial ocorre quando um atributo não-chave depende de APENAS PARTE de uma chave primária composta (não de toda a chave). Isso viola a 2FN porque essa forma normal exige que todo atributo não-chave dependa da chave primária INTEIRA — quando há dependência parcial, a tabela deve ser decomposta em tabelas menores, onde cada atributo dependa completamente da chave da tabela em que está.",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = transacoes,
                Enunciado = "Na sigla ACID (propriedades de uma transação em banco de dados), a letra \"I\" representa:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'C',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Integridade" },
                    new() { Letra = 'B', Texto = "Indexação" },
                    new() { Letra = 'C', Texto = "Isolamento (Isolation)" },
                    new() { Letra = 'D', Texto = "Independência" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = transacoes,
                Enunciado = "A propriedade \"Atomicidade\" garante que uma transação seja executada por completo (todas as operações) ou não seja executada de forma alguma (rollback total em caso de falha).",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = transacoes,
                Enunciado = "Um \"deadlock\" ocorre quando duas ou mais transações ficam bloqueadas esperando indefinidamente por um recurso (lock) que a outra transação está mantendo.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = transacoes,
                Enunciado = "Na sigla ACID, o que a letra \"D\" representa (a propriedade que garante que dados de uma transação confirmada não sejam perdidos, mesmo em caso de falha do sistema)?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Durabilidade",
            },
        };

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    private static async Task SeedSistemasOperacionaisAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Sistemas Operacionais");

        var processos = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Gerência de Processos e Escalonamento");
        var memoria = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Gerência de Memória");
        var concorrencia = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Concorrência e Sincronização");
        var arquivosES = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Sistemas de Arquivos e Entrada/Saída");

        var candidatas = new List<Questao>
        {
            new QuestaoMultiplaEscolha
            {
                Assunto = processos,
                Enunciado = "Qual algoritmo de escalonamento de processos executa sempre o processo com o menor tempo de execução estimado primeiro?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "FCFS (First-Come, First-Served)" },
                    new() { Letra = 'B', Texto = "SJF (Shortest Job First)" },
                    new() { Letra = 'C', Texto = "Round Robin" },
                    new() { Letra = 'D', Texto = "Prioridade fixa (sem preempção)" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = processos,
                Enunciado = "O algoritmo Round Robin utiliza um quantum de tempo fixo, alternando entre os processos prontos de forma cíclica — sendo especialmente adequado para sistemas de tempo compartilhado (time-sharing).",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = processos,
                Enunciado = "Um processo no estado \"Bloqueado\" (ou \"Esperando\") está pronto para ser executado assim que a CPU for liberada, bastando o escalonador escolhê-lo.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = processos,
                Enunciado = "Associe cada estado de um processo ao seu significado.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Novo", Correspondente = "Processo criado, ainda não admitido pelo escalonador" },
                    new() { Ordem = 1, Termo = "Pronto", Correspondente = "Aguardando a CPU ficar disponível" },
                    new() { Ordem = 2, Termo = "Executando", Correspondente = "Instruções sendo processadas pela CPU no momento" },
                    new() { Ordem = 3, Termo = "Bloqueado", Correspondente = "Aguardando um evento externo (ex.: E/S) para poder continuar" },
                },
            },
            new QuestaoDiscursiva
            {
                Assunto = processos,
                Enunciado = "Explique a diferença entre um processo e uma thread.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Um processo é uma instância de um programa em execução, com seu próprio espaço de memória isolado (endereçamento próprio), recursos alocados pelo sistema operacional e, geralmente, maior custo de criação/troca de contexto. Uma thread é uma unidade de execução DENTRO de um processo — múltiplas threads de um mesmo processo compartilham o mesmo espaço de memória e recursos, tornando a criação e a troca de contexto entre threads mais leve (mais barata) do que entre processos, mas exigindo cuidado com acesso concorrente a dados compartilhados.",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = memoria,
                Enunciado = "Memória Virtual é uma técnica que permite:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Executar processos maiores que a memória física disponível, usando disco como extensão" },
                    new() { Letra = 'B', Texto = "Aumentar fisicamente a quantidade de RAM instalada" },
                    new() { Letra = 'C', Texto = "Eliminar completamente a necessidade de memória RAM" },
                    new() { Letra = 'D', Texto = "Impedir que dois processos rodem ao mesmo tempo" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = memoria,
                Enunciado = "\"Paginação\" é uma técnica de gerência de memória que divide a memória física e lógica em blocos de tamanho fixo, chamados páginas/quadros.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = memoria,
                Enunciado = "\"Thrashing\" ocorre quando o sistema passa a maior parte do tempo trocando páginas entre memória e disco, em vez de executar processos de fato — geralmente causado por memória física insuficiente para a carga de trabalho.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = memoria,
                Enunciado = "Qual é o nome do algoritmo de substituição de páginas que remove sempre a página que não é usada há mais tempo?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "LRU (Least Recently Used)",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = concorrencia,
                Enunciado = "Uma \"condição de corrida\" (race condition) ocorre quando:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "O resultado de uma operação depende da ordem/tempo não determinístico de execução de processos/threads concorrentes" },
                    new() { Letra = 'B', Texto = "Um processo termina mais rápido que o esperado" },
                    new() { Letra = 'C', Texto = "Dois processos usam a mesma versão do sistema operacional" },
                    new() { Letra = 'D', Texto = "A CPU está ociosa por muito tempo" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = concorrencia,
                Enunciado = "Um semáforo (semaphore) é um mecanismo de sincronização que pode ser usado para controlar o acesso a uma seção crítica por múltiplos processos/threads.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = concorrencia,
                Enunciado = "Um Mutex (mutual exclusion) permite que múltiplas threads acessem simultaneamente a mesma seção crítica, sem restrição.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoDiscursiva
            {
                Assunto = concorrencia,
                Enunciado = "Descreva as quatro condições necessárias (condições de Coffman) para que ocorra um deadlock.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "(1) Exclusão mútua: pelo menos um recurso deve ser mantido em modo não compartilhável. (2) Posse e espera (hold and wait): um processo deve estar segurando pelo menos um recurso enquanto espera por outro. (3) Não preempção: um recurso só pode ser liberado voluntariamente pelo processo que o detém. (4) Espera circular: deve existir um conjunto de processos {P0, P1, ..., Pn} onde P0 espera um recurso de P1, P1 espera de P2, ..., e Pn espera de P0, formando um ciclo.",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = arquivosES,
                Enunciado = "Em um sistema de arquivos, o \"i-node\" (inode) tipicamente armazena:",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Metadados do arquivo (permissões, dono, tamanho, ponteiros para os blocos de dados)" },
                    new() { Letra = 'B', Texto = "O conteúdo textual completo do arquivo" },
                    new() { Letra = 'C', Texto = "Apenas o nome do arquivo" },
                    new() { Letra = 'D', Texto = "O histórico de versões do arquivo" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = arquivosES,
                Enunciado = "Operações de Entrada/Saída (E/S) costumam ser significativamente mais lentas que operações realizadas exclusivamente na CPU/memória, por isso sistemas operacionais usam técnicas como buffering e caching para mitigar esse impacto.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoRespostaBreve
            {
                Assunto = arquivosES,
                Enunciado = "Como se chama a área de memória usada para armazenar temporariamente dados sendo transferidos entre um dispositivo (mais lento) e a memória/CPU (mais rápida)?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "Buffer",
            },
        };

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    private static async Task SeedArquiteturaDeComputadoresAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Arquitetura de Computadores");

        var organizacao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Organização Básica e Hierarquia de Memória");
        var representacao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Representação de Dados e Aritmética Binária");
        var processadores = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Processadores e Pipeline");

        var candidatas = new List<Questao>
        {
            new QuestaoMultiplaEscolha
            {
                Assunto = organizacao,
                Enunciado = "Na arquitetura de von Neumann, dados e instruções de programa são armazenados:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Na mesma memória, compartilhando o mesmo espaço de endereçamento" },
                    new() { Letra = 'B', Texto = "Sempre em memórias fisicamente separadas" },
                    new() { Letra = 'C', Texto = "Apenas em disco, nunca em memória RAM" },
                    new() { Letra = 'D', Texto = "Diretamente nos registradores da CPU" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = organizacao,
                Enunciado = "Na hierarquia de memória, quanto mais próxima da CPU (ex.: registradores, cache L1), maior a velocidade de acesso e menor a capacidade de armazenamento, em geral.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = organizacao,
                Enunciado = "A memória cache é um tipo de memória não volátil, usada para armazenamento permanente de dados.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = organizacao,
                Enunciado = "Associe cada componente à sua função básica na arquitetura de um computador.",
                Dificuldade = Dificuldade.Media,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "ULA (Unidade Lógica e Aritmética)", Correspondente = "Executa operações aritméticas e lógicas" },
                    new() { Ordem = 1, Termo = "Unidade de Controle", Correspondente = "Coordena a busca, decodificação e execução de instruções" },
                    new() { Ordem = 2, Termo = "Registradores", Correspondente = "Armazenamento temporário, de altíssima velocidade, dentro da CPU" },
                    new() { Ordem = 3, Termo = "Barramento", Correspondente = "Conjunto de linhas que transportam dados/endereços/controle entre componentes" },
                },
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = representacao,
                Enunciado = "Quantos valores distintos podem ser representados com uma palavra binária de 8 bits (sem sinal)?",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "128" },
                    new() { Letra = 'B', Texto = "256" },
                    new() { Letra = 'C', Texto = "255" },
                    new() { Letra = 'D', Texto = "16" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = representacao,
                Enunciado = "No padrão IEEE 754 de ponto flutuante, um número é representado por sinal, expoente e mantissa (fração).",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },

            // Conversão decimal -> hexadecimal, calculada em tempo de execução.
        };

        var hexVariantes = new int[] { 255, 16, 100, 4096, 1024, 4, 200, 4080, 2, 65535, 32, 128 };
        for (var i = 0; i < hexVariantes.Length; i++)
        {
            var valorDecimal = hexVariantes[i];
            var hex = Convert.ToString(valorDecimal, 16).ToUpperInvariant();
            candidatas.Add(new QuestaoRespostaBreve
            {
                Assunto = representacao,
                Enunciado = $"Converta o número decimal {valorDecimal} para sua representação em hexadecimal (base 16).",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = hex,
            });
        }

        // Complemento de dois (8 bits) de um número negativo pequeno: representação = 256 - |n|.
        var complementoVariantes = new int[] { 1, 2, 3, 4, 5, 8, 10, 16, 20, 32, 50, 64 };
        for (var i = 0; i < complementoVariantes.Length; i++)
        {
            var n = complementoVariantes[i];
            var complemento = 256 - n;
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = representacao,
                Enunciado = $"Em um sistema de 8 bits usando complemento de dois, qual é o valor decimal SEM SINAL (0 a 255) que representa o número -{n}?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = complemento,
                Tolerancia = 0m,
            });
        }

        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = representacao,
            Enunciado = "Explique por que o complemento de dois é a forma mais usada para representar números inteiros com sinal em computadores, em vez de sinal-magnitude.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "O complemento de dois permite que a soma de números positivos e negativos seja feita usando o MESMO circuito somador usado para números sem sinal, sem necessidade de lógica especial para subtração ou para tratar o sinal separadamente — diferente da representação sinal-magnitude, que exige tratamento especial do bit de sinal e tem duas representações distintas para o zero (+0 e -0). O complemento de dois tem uma única representação para o zero e simplifica bastante o hardware aritmético.",
        });

        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = processadores,
            Enunciado = "\"Pipeline\" de instruções, em um processador, tem como principal objetivo:",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Aumentar o throughput de execução, sobrepondo as etapas de execução de instruções diferentes" },
                new() { Letra = 'B', Texto = "Reduzir o número de registradores necessários" },
                new() { Letra = 'C', Texto = "Eliminar a necessidade de memória cache" },
                new() { Letra = 'D', Texto = "Impedir que o processador execute mais de uma instrução por segundo" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = processadores,
            Enunciado = "Um \"hazard\" (risco) de pipeline ocorre quando a sobreposição de execução de instruções causaria um resultado incorreto se não fosse tratada (ex.: uma instrução depende do resultado de outra ainda não concluída).",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = processadores,
            Enunciado = "Processadores RISC (Reduced Instruction Set Computer) são caracterizados por um conjunto grande e complexo de instruções, cada uma capaz de executar várias operações.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = processadores,
            Enunciado = "Qual é a sigla usada para a arquitetura de processador caracterizada por um conjunto grande de instruções, muitas de execução complexa e multi-ciclo (o \"oposto\" filosófico do RISC)?",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "CISC (Complex Instruction Set Computer)",
        });

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }

    private static async Task SeedSegurancaDaInformacaoAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Segurança da Informação");

        var criptografia = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Criptografia");
        var principios = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Princípios de Segurança e Ataques Comuns");

        var candidatas = new List<Questao>
        {
            new QuestaoMultiplaEscolha
            {
                Assunto = criptografia,
                Enunciado = "Na criptografia SIMÉTRICA, a chave usada para cifrar e para decifrar a mensagem é:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "A mesma chave (compartilhada entre emissor e receptor)" },
                    new() { Letra = 'B', Texto = "Sempre chaves diferentes (uma pública, uma privada)" },
                    new() { Letra = 'C', Texto = "Gerada aleatoriamente a cada byte transmitido, sem repetição" },
                    new() { Letra = 'D', Texto = "Nunca conhecida por nenhuma das partes" },
                },
            },
            new QuestaoMultiplaEscolha
            {
                Assunto = criptografia,
                Enunciado = "Na criptografia ASSIMÉTRICA (de chave pública), o que é cifrado com a chave PÚBLICA do destinatário só pode ser decifrado com:",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = 'B',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "A mesma chave pública" },
                    new() { Letra = 'B', Texto = "A chave PRIVADA correspondente do destinatário" },
                    new() { Letra = 'C', Texto = "A chave pública do remetente" },
                    new() { Letra = 'D', Texto = "Qualquer chave pública disponível" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = criptografia,
                Enunciado = "Um \"hash\" criptográfico é uma função de mão única (não reversível), usada para verificar a integridade de dados, e não para cifrar/decifrar mensagens.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = criptografia,
                Enunciado = "Uma assinatura digital pode ser usada para garantir tanto a autenticidade (quem enviou) quanto o não-repúdio (o remetente não pode negar ter enviado) de uma mensagem.",
                Dificuldade = Dificuldade.Dificil,
                RespostaCorreta = true,
            },
            new QuestaoDiscursiva
            {
                Assunto = criptografia,
                Enunciado = "Compare a criptografia simétrica e a assimétrica quanto a desempenho e gerenciamento de chaves, e explique por que sistemas reais (como o HTTPS) costumam combinar as duas.",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Criptografia simétrica é computacionalmente mais rápida, mas exige que ambas as partes compartilhem previamente a mesma chave secreta de forma segura (problema de distribuição de chaves). Criptografia assimétrica resolve o problema de distribuição (a chave pública pode ser divulgada livremente), mas é significativamente mais lenta/custosa computacionalmente. Por isso, protocolos como o HTTPS/TLS usam uma abordagem híbrida: a criptografia assimétrica é usada apenas na fase inicial do handshake, para negociar com segurança uma chave simétrica compartilhada (a \"chave de sessão\"), que passa a ser usada para cifrar o restante da comunicação com desempenho muito melhor.",
            },
            new QuestaoRespostaBreve
            {
                Assunto = criptografia,
                Enunciado = "Qual é o nome do algoritmo de criptografia assimétrica mais tradicional/conhecido, baseado na dificuldade de fatorar números primos grandes?",
                Dificuldade = Dificuldade.Media,
                RespostaEsperada = "RSA",
            },

            new QuestaoMultiplaEscolha
            {
                Assunto = principios,
                Enunciado = "Os três pilares clássicos da Segurança da Informação (tríade CIA/CID) são:",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = 'A',
                Alternativas = new()
                {
                    new() { Letra = 'A', Texto = "Confidencialidade, Integridade e Disponibilidade" },
                    new() { Letra = 'B', Texto = "Confidencialidade, Indexação e Duplicação" },
                    new() { Letra = 'C', Texto = "Criptografia, Isolamento e Detecção" },
                    new() { Letra = 'D', Texto = "Compressão, Integração e Distribuição" },
                },
            },
            new QuestaoCertoErrado
            {
                Assunto = principios,
                Enunciado = "Um ataque de \"phishing\" tenta enganar a vítima para que ela revele informações sensíveis (senhas, dados bancários), geralmente se passando por uma entidade confiável.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = principios,
                Enunciado = "Um ataque de \"SQL Injection\" explora a falta de sanitização/parametrização adequada de entradas do usuário, injetando comandos SQL maliciosos em uma consulta.",
                Dificuldade = Dificuldade.Media,
                RespostaCorreta = true,
            },
            new QuestaoCertoErrado
            {
                Assunto = principios,
                Enunciado = "A autenticação de dois fatores (2FA) exige apenas que o usuário digite a senha duas vezes seguidas, para reduzir erros de digitação.",
                Dificuldade = Dificuldade.Facil,
                RespostaCorreta = false,
            },
            new QuestaoAssociacao
            {
                Assunto = principios,
                Enunciado = "Associe cada tipo de ataque/ameaça à sua descrição.",
                Dificuldade = Dificuldade.Dificil,
                Pares = new()
                {
                    new() { Ordem = 0, Termo = "Phishing", Correspondente = "Engana a vítima para revelar dados sensíveis, se passando por confiável" },
                    new() { Ordem = 1, Termo = "SQL Injection", Correspondente = "Injeta comandos SQL maliciosos via entrada não sanitizada" },
                    new() { Ordem = 2, Termo = "Cross-Site Scripting (XSS)", Correspondente = "Injeta script malicioso que é executado no navegador de outros usuários" },
                    new() { Ordem = 3, Termo = "Ataque de força bruta", Correspondente = "Testa exaustivamente combinações de senha até acertar" },
                },
            },
            new QuestaoRespostaBreve
            {
                Assunto = principios,
                Enunciado = "Qual é a sigla do princípio de segurança que recomenda conceder a cada usuário/processo apenas as permissões estritamente necessárias para realizar sua função, nada além disso?",
                Dificuldade = Dificuldade.Dificil,
                RespostaEsperada = "Princípio do Menor Privilégio",
            },
        };

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
