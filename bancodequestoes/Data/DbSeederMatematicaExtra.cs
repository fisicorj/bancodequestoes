using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Estende "Matemática" com Assuntos novos, reaproveitando a mesma Disciplina
// por nome (ObterOuCriarDisciplinaAsync não duplica).
public static class DbSeederMatematicaExtra
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Matemática");

        var progressoes = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Progressões (PA e PG)");
        var combinatoria = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Análise Combinatória e Probabilidade");
        var matrizes = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Matrizes e Determinantes");
        var logaritmos = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Logaritmos e Exponenciais");
        var geometriaAnalitica = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Geometria Analítica");

        var candidatas = new List<Questao>();

        // ---------------- Progressões (PA e PG) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = progressoes,
            Enunciado = "Em uma Progressão Aritmética (PA), a diferença entre um termo e o termo anterior é:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Constante (a razão da PA)" },
                new() { Letra = 'B', Texto = "Sempre igual a zero" },
                new() { Letra = 'C', Texto = "Sempre crescente" },
                new() { Letra = 'D', Texto = "Igual ao primeiro termo" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = progressoes,
            Enunciado = "Em uma Progressão Geométrica (PG), cada termo é obtido multiplicando o termo anterior por uma razão constante.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = progressoes,
            Enunciado = "Escreva a fórmula do termo geral de uma PA e de uma PG, definindo cada símbolo utilizado.",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "PA: an = a1 + (n-1)·r, onde an é o n-ésimo termo, a1 é o primeiro termo, n é a posição do termo e r é a razão (diferença constante entre termos consecutivos). PG: an = a1 · q^(n-1), onde q é a razão (constante multiplicativa entre termos consecutivos).",
        });

        // PA: termo geral an = a1 + (n-1)*r
        var paVariantes = new (int A1, int R, int N)[]
        {
            (2, 3, 10), (5, 2, 8), (1, 4, 6), (10, -2, 5), (3, 5, 7), (0, 6, 9), (7, 1, 12), (4, 3, 15), (2, 7, 4), (8, -1, 10), (1, 2, 20), (6, 4, 6),
        };
        for (var i = 0; i < paVariantes.Length; i++)
        {
            var (a1, r, n) = paVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = progressoes,
                Enunciado = $"Em uma PA de primeiro termo a1 = {a1} e razão r = {r}, qual é o valor do {n}º termo (a{n})?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = a1 + (n - 1) * r,
                Tolerancia = 0m,
            });
        }

        // Soma dos n primeiros termos de uma PA: Sn = n*(a1+an)/2
        var somaPaVariantes = new (int A1, int R, int N)[]
        {
            (1, 1, 10), (2, 2, 5), (5, 3, 6), (0, 4, 8), (3, 1, 20), (1, 2, 15), (4, 5, 4), (2, 3, 10), (10, -1, 6), (1, 1, 100), (6, 2, 9), (3, 4, 7),
        };
        for (var i = 0; i < somaPaVariantes.Length; i++)
        {
            var (a1, r, n) = somaPaVariantes[i];
            var an = a1 + (n - 1) * r;
            var soma = n * (a1 + an) / 2;
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = progressoes,
                Enunciado = $"Qual é a soma dos {n} primeiros termos de uma PA de primeiro termo a1 = {a1} e razão r = {r}?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = soma,
                Tolerancia = 0m,
            });
        }

        // PG: termo geral an = a1 * q^(n-1) — q e n pequenos pra não explodir.
        var pgVariantes = new (int A1, int Q, int N)[]
        {
            (1, 2, 5), (2, 3, 4), (1, 2, 8), (3, 2, 4), (1, 3, 4), (2, 2, 6), (5, 2, 4), (1, 4, 4), (4, 2, 5), (1, 5, 3), (2, 4, 3), (3, 3, 3),
        };
        for (var i = 0; i < pgVariantes.Length; i++)
        {
            var (a1, q, n) = pgVariantes[i];
            var an = a1 * (long)Math.Pow(q, n - 1);
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = progressoes,
                Enunciado = $"Em uma PG de primeiro termo a1 = {a1} e razão q = {q}, qual é o valor do {n}º termo (a{n})?",
                Dificuldade = (Dificuldade)((i + 2) % 3),
                RespostaEsperada = an,
                Tolerancia = 0m,
            });
        }

        // ---------------- Análise Combinatória e Probabilidade ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = combinatoria,
            Enunciado = "Um Arranjo se diferencia de uma Combinação principalmente porque, no Arranjo:",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "A ordem dos elementos escolhidos importa" },
                new() { Letra = 'B', Texto = "A ordem dos elementos escolhidos nunca importa" },
                new() { Letra = 'C', Texto = "Elementos podem se repetir livremente" },
                new() { Letra = 'D', Texto = "Não é possível escolher todos os elementos do conjunto" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = combinatoria,
            Enunciado = "A probabilidade de um evento sempre está entre 0 e 1 (podendo ser expressa também como uma porcentagem entre 0% e 100%).",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = combinatoria,
            Enunciado = "Explique a diferença entre Permutação, Arranjo e Combinação, com um exemplo de cada.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Permutação: agrupamentos que usam TODOS os elementos de um conjunto, variando a ordem — ex.: quantas formas de organizar 5 pessoas em fila (P5 = 5!). Arranjo: agrupamentos que usam PARTE dos elementos, onde a ORDEM importa — ex.: quantas formas de eleger um presidente e um vice entre 5 candidatos (A5,2). Combinação: agrupamentos que usam PARTE dos elementos, onde a ORDEM NÃO importa — ex.: quantas formas de escolher uma comissão de 2 pessoas entre 5 candidatos (C5,2).",
        });

        // Permutação simples: P(n) = n!
        var permutacaoVariantes = new int[] { 3, 4, 5, 6, 2, 7, 4, 5, 3, 6, 4, 5 };
        for (var i = 0; i < permutacaoVariantes.Length; i++)
        {
            var n = permutacaoVariantes[i];
            long fatorial = 1;
            for (var k = 2; k <= n; k++)
            {
                fatorial *= k;
            }

            candidatas.Add(new QuestaoNumerica
            {
                Assunto = combinatoria,
                Enunciado = $"De quantas formas diferentes é possível organizar, em fila, {n} pessoas distintas (considerando todas as ordens possíveis)?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = fatorial,
                Tolerancia = 0m,
            });
        }

        // Combinação simples: C(n,k) = n! / (k! * (n-k)!)
        var combinacaoVariantes = new (int N, int K)[]
        {
            (5, 2), (6, 3), (4, 2), (7, 2), (5, 3), (8, 2), (6, 2), (10, 2), (4, 1), (9, 2), (6, 4), (7, 3),
        };
        for (var i = 0; i < combinacaoVariantes.Length; i++)
        {
            var (n, k) = combinacaoVariantes[i];
            long Fatorial(int valor)
            {
                long resultado = 1;
                for (var v = 2; v <= valor; v++)
                {
                    resultado *= v;
                }

                return resultado;
            }

            var combinacao = Fatorial(n) / (Fatorial(k) * Fatorial(n - k));
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = combinatoria,
                Enunciado = $"Quantas comissões diferentes de {k} pessoas é possível formar a partir de um grupo de {n} pessoas (a ordem de escolha não importa)?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = combinacao,
                Tolerancia = 0m,
            });
        }

        // ---------------- Matrizes e Determinantes ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = matrizes,
            Enunciado = "Uma matriz é chamada de \"quadrada\" quando:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "O número de linhas é igual ao número de colunas" },
                new() { Letra = 'B', Texto = "Todos os elementos são iguais" },
                new() { Letra = 'C', Texto = "Ela tem determinante igual a zero" },
                new() { Letra = 'D', Texto = "Ela possui apenas uma linha" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = matrizes,
            Enunciado = "Uma matriz cujo determinante é igual a zero é chamada de matriz singular e não possui matriz inversa.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = matrizes,
            Enunciado = "Descreva a regra prática (regra de Sarrus) para calcular o determinante de uma matriz 3×3.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "A Regra de Sarrus consiste em repetir as duas primeiras colunas da matriz à direita da terceira coluna, formando 5 colunas. Somam-se os produtos das três diagonais \"principais\" (de cima-esquerda para baixo-direita) e subtraem-se os produtos das três diagonais \"secundárias\" (de baixo-esquerda para cima-direita). O resultado dessa soma/subtração é o determinante da matriz 3×3.",
        });

        // Determinante 2x2: det = a*d - b*c
        var det2x2Variantes = new (int A, int B, int C, int D)[]
        {
            (2, 1, 3, 4), (5, 2, 1, 3), (1, 0, 0, 1), (3, 4, 2, 5), (6, 1, 2, 3), (4, 2, 1, 5),
            (7, 3, 2, 4), (2, 5, 1, 6), (8, 1, 3, 2), (1, 1, 1, 2), (9, 2, 4, 3), (3, 2, 5, 4),
        };
        for (var i = 0; i < det2x2Variantes.Length; i++)
        {
            var (a, b, c, d) = det2x2Variantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = matrizes,
                Enunciado = $"Qual é o determinante da matriz 2×2 [[{a}, {b}], [{c}, {d}]] (primeira linha: {a}, {b}; segunda linha: {c}, {d})?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = (a * d) - (b * c),
                Tolerancia = 0m,
            });
        }

        // ---------------- Logaritmos e Exponenciais ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = logaritmos,
            Enunciado = "A expressão log_b(x) = y é equivalente a:",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "b^y = x" },
                new() { Letra = 'B', Texto = "y^b = x" },
                new() { Letra = 'C', Texto = "x^y = b" },
                new() { Letra = 'D', Texto = "b × y = x" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = logaritmos,
            Enunciado = "O logaritmo de um número negativo, em base real positiva, não é definido nos números reais.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = logaritmos,
            Enunciado = "Qual é o valor de log₁₀(1) (logaritmo de 1 em qualquer base)?",
            Dificuldade = Dificuldade.Facil,
            RespostaEsperada = "0",
        });

        // Logaritmo com resultado inteiro exato: log_base(base^expoente) = expoente.
        var logVariantes = new (int Base, int Expoente)[]
        {
            (2, 3), (2, 5), (3, 2), (3, 4), (5, 2), (10, 2), (10, 3), (2, 8), (4, 2), (2, 10), (5, 3), (7, 2),
        };
        for (var i = 0; i < logVariantes.Length; i++)
        {
            var (baseNum, expoente) = logVariantes[i];
            var potencia = (long)Math.Pow(baseNum, expoente);
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = logaritmos,
                Enunciado = $"Qual é o valor de log base {baseNum} de {potencia} (isto é, log_{baseNum}({potencia}))?",
                Dificuldade = (Dificuldade)((i + 2) % 3),
                RespostaEsperada = expoente,
                Tolerancia = 0m,
            });
        }

        // ---------------- Geometria Analítica ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = geometriaAnalitica,
            Enunciado = "No plano cartesiano, a distância entre dois pontos A(x1, y1) e B(x2, y2) é calculada por:",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "√[(x2-x1)² + (y2-y1)²]" },
                new() { Letra = 'B', Texto = "(x2-x1) + (y2-y1)" },
                new() { Letra = 'C', Texto = "(x2-x1) × (y2-y1)" },
                new() { Letra = 'D', Texto = "√(x2×x1 + y2×y1)" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = geometriaAnalitica,
            Enunciado = "O coeficiente angular (inclinação) de uma reta é constante em toda a sua extensão.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = geometriaAnalitica,
            Enunciado = "Descreva como calcular o ponto médio de um segmento de reta definido por dois pontos A(x1, y1) e B(x2, y2).",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "O ponto médio M de um segmento AB é dado por M = ((x1+x2)/2, (y1+y2)/2), ou seja, a média aritmética das coordenadas x e a média aritmética das coordenadas y dos dois pontos extremos.",
        });

        // Distância entre pontos, usando triplas pitagóricas (3,4,5), (6,8,10), (5,12,13), (8,15,17) pra resultado exato.
        var distanciaVariantes = new (int X1, int Y1, int X2, int Y2, int DistanciaEsperada)[]
        {
            (0, 0, 3, 4, 5), (0, 0, 6, 8, 10), (1, 1, 6, 13, 13), (2, 3, 10, 18, 17), (0, 0, 5, 12, 13),
            (0, 0, 8, 15, 17), (1, 2, 4, 6, 5), (3, 3, 9, 11, 10), (0, 0, 9, 12, 15), (2, 1, 14, 6, 13),
            (1, 1, 4, 5, 5), (0, 5, 12, 5 + 5, 13),
        };
        for (var i = 0; i < distanciaVariantes.Length; i++)
        {
            var (x1, y1, x2, y2, distanciaEsperada) = distanciaVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = geometriaAnalitica,
                Enunciado = $"No plano cartesiano, qual é a distância entre os pontos A({x1}, {y1}) e B({x2}, {y2})?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = distanciaEsperada,
                Tolerancia = 0m,
            });
        }

        // Ponto médio: coordenadas escolhidas pra resultado sempre inteiro (soma par).
        var pontoMedioVariantes = new (int X1, int Y1, int X2, int Y2)[]
        {
            (0, 0, 4, 6), (2, 2, 8, 10), (1, 3, 7, 9), (0, 4, 10, 8), (3, 1, 9, 7), (2, 6, 12, 4),
            (1, 1, 9, 9), (0, 0, 20, 10), (4, 2, 10, 8), (5, 5, 15, 15), (2, 8, 6, 0), (3, 7, 11, 3),
        };
        for (var i = 0; i < pontoMedioVariantes.Length; i++)
        {
            var (x1, y1, x2, y2) = pontoMedioVariantes[i];
            var mx = (x1 + x2) / 2;
            var my = (y1 + y2) / 2;
            candidatas.Add(new QuestaoRespostaBreve
            {
                Assunto = geometriaAnalitica,
                Enunciado = $"Qual é o ponto médio do segmento definido pelos pontos A({x1}, {y1}) e B({x2}, {y2})? Informe no formato (x, y).",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = $"({mx}, {my})",
            });
        }

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
