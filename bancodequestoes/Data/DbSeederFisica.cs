using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Disciplina Física (6 Assuntos); questões numéricas geradas por loop com a
// conta feita em C# (ex.: F = m * a), nunca à mão.
public static class DbSeederFisica
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Física");

        var cinematica = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Cinemática");
        var dinamica = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Dinâmica e Leis de Newton");
        var energia = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Trabalho, Energia e Potência");
        var termologia = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Termologia");
        var eletricidade = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Eletricidade e Circuitos");
        var ondulatoria = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Ondulatória e Óptica");

        var candidatas = new List<Questao>();

        // ---------------- Cinemática (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = cinematica,
            Enunciado = "No Movimento Retilíneo Uniforme (MRU), a velocidade do móvel é:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Constante ao longo do tempo" },
                new() { Letra = 'B', Texto = "Sempre crescente" },
                new() { Letra = 'C', Texto = "Sempre igual a zero" },
                new() { Letra = 'D', Texto = "Proporcional ao quadrado do tempo" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = cinematica,
            Enunciado = "No Movimento Retilíneo Uniformemente Variado (MRUV), a aceleração é constante e diferente de zero.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = cinematica,
            Enunciado = "Velocidade escalar média é definida como a distância total percorrida dividida pelo tempo total gasto no percurso.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = cinematica,
            Enunciado = "Explique a diferença entre velocidade média e velocidade instantânea.",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Velocidade média é a razão entre o deslocamento total e o intervalo de tempo total do percurso, sem considerar as variações ao longo do trajeto. Velocidade instantânea é a velocidade do móvel em um ponto específico do tempo (o limite da velocidade média quando o intervalo de tempo tende a zero), podendo variar a cada instante mesmo que a velocidade média seja constante.",
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = cinematica,
            Enunciado = "Associe cada tipo de movimento à sua característica principal.",
            Dificuldade = Dificuldade.Media,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "MRU", Correspondente = "Velocidade constante, aceleração nula" },
                new() { Ordem = 1, Termo = "MRUV", Correspondente = "Aceleração constante e diferente de zero" },
                new() { Ordem = 2, Termo = "Queda livre", Correspondente = "MRUV com aceleração igual à gravidade" },
                new() { Ordem = 3, Termo = "Movimento circular uniforme", Correspondente = "Velocidade escalar constante, direção variável" },
            },
        });

        // MRU: v = d / t (km/h) — pares (d, t) escolhidos pra divisão exata.
        var mruVariantes = new (int DistanciaKm, int TempoH)[]
        {
            (120, 2), (90, 3), (150, 5), (240, 4), (60, 1), (300, 6), (80, 2), (210, 3), (45, 1), (400, 8), (36, 2), (500, 10),
        };
        for (var i = 0; i < mruVariantes.Length; i++)
        {
            var (d, t) = mruVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = cinematica,
                Enunciado = $"Um carro percorre {d} km em {t} horas, com velocidade constante. Qual é a velocidade média desse carro, em km/h?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = d / t,
                Tolerancia = 0m,
            });
        }

        // Queda livre (g = 10 m/s²): v = g * t
        var quedaLivreVariantes = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        foreach (var (t, i) in quedaLivreVariantes.Select((t, i) => (t, i)))
        {
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = cinematica,
                Enunciado = $"Um objeto é solto em queda livre, partindo do repouso (g = 10 m/s², despreze a resistência do ar). Qual é a velocidade do objeto, em m/s, após {t} segundo(s) de queda?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = 10 * t,
                Tolerancia = 0m,
            });
        }

        // ---------------- Dinâmica (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = dinamica,
            Enunciado = "A Segunda Lei de Newton estabelece que a força resultante sobre um corpo é igual a:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Massa dividida pela aceleração" },
                new() { Letra = 'B', Texto = "Massa multiplicada pela aceleração" },
                new() { Letra = 'C', Texto = "Peso dividido pela massa" },
                new() { Letra = 'D', Texto = "Velocidade multiplicada pelo tempo" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = dinamica,
            Enunciado = "A Primeira Lei de Newton (Lei da Inércia) afirma que um corpo em repouso ou em movimento retilíneo uniforme tende a manter esse estado, a menos que uma força resultante não nula atue sobre ele.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = dinamica,
            Enunciado = "Pela Terceira Lei de Newton, ação e reação atuam sempre no mesmo corpo, por isso se anulam.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = dinamica,
            Enunciado = "No Sistema Internacional (SI), qual é a unidade de medida de força?",
            Dificuldade = Dificuldade.Facil,
            RespostaEsperada = "Newton (N)",
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = dinamica,
            Enunciado = "Um bloco está em repouso sobre uma mesa horizontal. Identifique as forças que atuam sobre o bloco e explique por que ele permanece em equilíbrio.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Atuam sobre o bloco o peso (força gravitacional, para baixo) e a força normal exercida pela mesa (para cima). O bloco permanece em equilíbrio porque essas duas forças têm mesma intensidade e direção, mas sentidos opostos, de modo que a força resultante sobre o bloco é nula (1ª Lei de Newton).",
        });

        // F = m * a (N) — massa (kg) e aceleração (m/s²).
        var forcaVariantes = new (int MassaKg, int AceleracaoMs2)[]
        {
            (10, 2), (5, 4), (20, 3), (8, 5), (15, 2), (6, 6), (12, 4), (25, 2), (3, 9), (18, 5), (7, 8), (30, 3),
        };
        for (var i = 0; i < forcaVariantes.Length; i++)
        {
            var (m, a) = forcaVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = dinamica,
                Enunciado = $"Qual é a intensidade da força resultante (em N) que atua sobre um corpo de massa {m} kg, sabendo que ele adquire uma aceleração de {a} m/s²?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = m * a,
                Tolerancia = 0m,
            });
        }

        // ---------------- Trabalho, Energia e Potência (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = energia,
            Enunciado = "No Sistema Internacional, a unidade de medida de energia (e de trabalho) é:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Joule (J)" },
                new() { Letra = 'B', Texto = "Newton (N)" },
                new() { Letra = 'C', Texto = "Watt (W)" },
                new() { Letra = 'D', Texto = "Pascal (Pa)" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = energia,
            Enunciado = "O Princípio da Conservação da Energia Mecânica afirma que, na ausência de forças dissipativas (como o atrito), a energia mecânica total de um sistema permanece constante.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoLacunas
        {
            Assunto = energia,
            Enunciado = "Potência é definida como a razão entre o ___ realizado e o ___ gasto para realizá-lo.",
            Dificuldade = Dificuldade.Media,
            Lacunas = new()
            {
                new() { Ordem = 0, RespostaEsperada = "trabalho" },
                new() { Ordem = 1, RespostaEsperada = "tempo" },
            },
        });

        // Trabalho: W = F * d (J) — força (N) e deslocamento (m), na mesma direção.
        var trabalhoVariantes = new (int ForcaN, int DeslocamentoM)[]
        {
            (10, 5), (20, 3), (15, 4), (8, 10), (25, 2), (12, 6), (30, 5), (6, 8), (40, 2), (18, 4), (9, 7), (50, 3),
        };
        for (var i = 0; i < trabalhoVariantes.Length; i++)
        {
            var (f, d) = trabalhoVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = energia,
                Enunciado = $"Uma força constante de {f} N desloca um corpo por {d} metros, na mesma direção e sentido da força. Qual é o trabalho realizado por essa força, em joules?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = f * d,
                Tolerancia = 0m,
            });
        }

        // Energia cinética: Ec = m * v² / 2 (J) — v par, pra resultado inteiro.
        var energiaCineticaVariantes = new (int MassaKg, int VelocidadeMs)[]
        {
            (2, 4), (4, 2), (1, 6), (5, 4), (2, 6), (10, 2), (3, 4), (8, 2), (1, 10), (4, 4), (2, 8), (5, 2),
        };
        for (var i = 0; i < energiaCineticaVariantes.Length; i++)
        {
            var (m, v) = energiaCineticaVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = energia,
                Enunciado = $"Qual é a energia cinética, em joules, de um corpo de massa {m} kg que se move com velocidade de {v} m/s (Ec = m·v²/2)?",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = m * v * v / 2m,
                Tolerancia = 0m,
            });
        }

        // ---------------- Termologia (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = termologia,
            Enunciado = "Na Escala Celsius, a que temperatura a água entra em ebulição, ao nível do mar?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'C',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "0°C" },
                new() { Letra = 'B', Texto = "37°C" },
                new() { Letra = 'C', Texto = "100°C" },
                new() { Letra = 'D', Texto = "212°C" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = termologia,
            Enunciado = "Calor é uma forma de energia em trânsito, que flui espontaneamente do corpo de maior temperatura para o de menor temperatura.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = termologia,
            Enunciado = "Durante uma mudança de estado físico (ex.: fusão do gelo), a temperatura da substância continua aumentando enquanto ela absorve calor.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = termologia,
            Enunciado = "Explique a diferença entre condução, convecção e irradiação como formas de propagação de calor.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Condução: transferência de calor por contato direto entre partículas de um meio material (sólido, geralmente), sem transporte de matéria — ex.: uma colher de metal esquentando ao ser colocada num líquido quente. Convecção: transferência de calor através do movimento de massas de um fluido (líquido ou gás), com transporte de matéria — ex.: correntes de ar quente subindo. Irradiação: transferência de calor por meio de ondas eletromagnéticas, sem necessidade de meio material — ex.: o calor do Sol chegando à Terra através do vácuo.",
        });

        // Calor sensível simplificado (calor específico da água = 1 cal/g°C): Q = m * ΔT
        var calorVariantes = new (int MassaG, int T1, int T2)[]
        {
            (100, 20, 40), (200, 25, 45), (50, 10, 30), (300, 20, 25), (150, 15, 35), (100, 30, 80),
            (250, 20, 30), (400, 10, 20), (80, 25, 65), (500, 20, 22), (120, 18, 38), (60, 24, 74),
        };
        for (var i = 0; i < calorVariantes.Length; i++)
        {
            var (m, t1, t2) = calorVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = termologia,
                Enunciado = $"Considerando o calor específico da água igual a 1 cal/g°C, qual é a quantidade de calor (em calorias) necessária para aquecer {m} g de água de {t1}°C para {t2}°C?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = m * (t2 - t1),
                Tolerancia = 0m,
            });
        }

        // ---------------- Eletricidade e Circuitos (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = eletricidade,
            Enunciado = "A Primeira Lei de Ohm relaciona tensão (V), resistência (R) e corrente elétrica (I) pela fórmula:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "V = R × I" },
                new() { Letra = 'B', Texto = "V = R / I" },
                new() { Letra = 'C', Texto = "V = R + I" },
                new() { Letra = 'D', Texto = "V = I² × R²" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = eletricidade,
            Enunciado = "Em um circuito puramente em série, a corrente elétrica é a mesma em todos os componentes do circuito.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = eletricidade,
            Enunciado = "Em um circuito puramente em paralelo, a tensão é a mesma em todos os ramos do circuito.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = eletricidade,
            Enunciado = "No Sistema Internacional, qual é a unidade de medida de resistência elétrica?",
            Dificuldade = Dificuldade.Facil,
            RespostaEsperada = "Ohm (Ω)",
        });

        // Lei de Ohm: V = R * I (volts)
        var ohmVariantes = new (int ResistenciaOhm, int CorrenteA)[]
        {
            (10, 2), (5, 4), (20, 1), (8, 5), (100, 1), (15, 2), (4, 6), (25, 4), (12, 5), (6, 10), (50, 2), (30, 3),
        };
        for (var i = 0; i < ohmVariantes.Length; i++)
        {
            var (r, corrente) = ohmVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = eletricidade,
                Enunciado = $"Um resistor de {r} Ω é percorrido por uma corrente elétrica de {corrente} A. Qual é a tensão (ddp), em volts, sobre esse resistor (V = R·I)?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = r * corrente,
                Tolerancia = 0m,
            });
        }

        // Potência elétrica: P = V * I (watts)
        var potenciaEletricaVariantes = new (int TensaoV, int CorrenteA)[]
        {
            (110, 2), (220, 1), (12, 5), (5, 4), (100, 3), (24, 2), (127, 2), (9, 6), (60, 5), (200, 2), (18, 10), (36, 3),
        };
        for (var i = 0; i < potenciaEletricaVariantes.Length; i++)
        {
            var (v, corrente) = potenciaEletricaVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = eletricidade,
                Enunciado = $"Um aparelho elétrico é ligado a uma tensão de {v} V e é percorrido por uma corrente de {corrente} A. Qual é a potência elétrica consumida, em watts (P = V·I)?",
                Dificuldade = (Dificuldade)((i + 2) % 3),
                RespostaEsperada = v * corrente,
                Tolerancia = 0m,
            });
        }

        // ---------------- Ondulatória e Óptica (conceituais) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = ondulatoria,
            Enunciado = "A equação fundamental da ondulatória relaciona a velocidade de propagação (v), a frequência (f) e o comprimento de onda (λ) por:",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "v = f / λ" },
                new() { Letra = 'B', Texto = "v = f × λ" },
                new() { Letra = 'C', Texto = "v = f + λ" },
                new() { Letra = 'D', Texto = "v = λ / f²" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = ondulatoria,
            Enunciado = "O som não se propaga no vácuo, pois é uma onda mecânica e precisa de um meio material para se propagar.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = ondulatoria,
            Enunciado = "A luz visível é uma onda mecânica, assim como o som.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = ondulatoria,
            Enunciado = "Explique o fenômeno da reflexão total da luz e cite uma aplicação prática desse fenômeno.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "A reflexão total ocorre quando a luz, ao passar de um meio mais refringente para um meio menos refringente, incide na superfície de separação com um ângulo maior que o ângulo-limite: toda a luz é refletida, sem refração. Aplicação prática: fibras ópticas, onde a luz é conduzida por reflexões totais sucessivas ao longo do núcleo da fibra, usadas em telecomunicações (internet, telefonia) para transmitir dados com alta velocidade e baixa perda de sinal.",
        });

        // v = f * λ (m/s) — frequência (Hz) e comprimento de onda (m).
        var ondaVariantes = new (int FrequenciaHz, int ComprimentoM)[]
        {
            (10, 2), (5, 4), (20, 3), (2, 10), (50, 1), (4, 5), (100, 2), (8, 5), (25, 4), (1, 20), (6, 6), (15, 2),
        };
        for (var i = 0; i < ondaVariantes.Length; i++)
        {
            var (freq, comprimento) = ondaVariantes[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = ondulatoria,
                Enunciado = $"Uma onda periódica tem frequência de {freq} Hz e comprimento de onda de {comprimento} metros. Qual é a velocidade de propagação dessa onda, em m/s (v = f·λ)?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = freq * comprimento,
                Tolerancia = 0m,
            });
        }

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
