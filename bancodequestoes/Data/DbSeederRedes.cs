using BancoQuestoes.Models;

namespace BancoQuestoes.Data;

// Disciplina Redes de Computadores, mesmo padrão de DbSeederFisica.cs.
public static class DbSeederRedes
{
    public static async Task SeedAsync(ApplicationDbContext db, string? criadoPorId)
    {
        var disciplina = await DbSeeder.ObterOuCriarDisciplinaAsync(db, "Redes de Computadores");

        var modelos = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Modelos OSI e TCP/IP");
        var enlaceFisica = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Camadas Física e de Enlace");
        var redeRoteamento = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Camada de Rede, IP e Roteamento");
        var transporte = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Camada de Transporte (TCP/UDP)");
        var aplicacao = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Camada de Aplicação e Protocolos");
        var segurancaRedes = DbSeeder.ObterOuCriarAssunto(db, disciplina, "Segurança de Redes e Redes Sem Fio");

        var candidatas = new List<Questao>();

        // ---------------- Modelos OSI e TCP/IP ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = modelos,
            Enunciado = "Quantas camadas compõem o modelo de referência OSI?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'C',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "4" },
                new() { Letra = 'B', Texto = "5" },
                new() { Letra = 'C', Texto = "7" },
                new() { Letra = 'D', Texto = "9" },
            },
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = modelos,
            Enunciado = "Associe cada camada do modelo OSI à sua principal responsabilidade.",
            Dificuldade = Dificuldade.Media,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "Camada Física", Correspondente = "Transmissão de bits brutos pelo meio físico" },
                new() { Ordem = 1, Termo = "Camada de Enlace", Correspondente = "Comunicação entre nós adjacentes, controle de erro" },
                new() { Ordem = 2, Termo = "Camada de Rede", Correspondente = "Roteamento de pacotes entre redes distintas" },
                new() { Ordem = 3, Termo = "Camada de Transporte", Correspondente = "Entrega fim a fim, confiabilidade" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = modelos,
            Enunciado = "O modelo TCP/IP, usado de fato na Internet, possui exatamente as mesmas sete camadas do modelo OSI, apenas com nomes diferentes.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoLacunas
        {
            Assunto = modelos,
            Enunciado = "No modelo TCP/IP simplificado, as quatro camadas são: Aplicação, ___, ___ e Acesso à Rede (ou Interface de Rede).",
            Dificuldade = Dificuldade.Media,
            Lacunas = new()
            {
                new() { Ordem = 0, RespostaEsperada = "Transporte" },
                new() { Ordem = 1, RespostaEsperada = "Internet (ou Rede)" },
            },
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = modelos,
            Enunciado = "Explique o conceito de encapsulamento no modelo em camadas de redes de computadores.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Encapsulamento é o processo pelo qual cada camada do modelo adiciona seu próprio cabeçalho (e, às vezes, trailer) aos dados recebidos da camada superior, antes de repassá-los à camada inferior. Por exemplo, a camada de Transporte encapsula os dados da Aplicação em um segmento (adicionando cabeçalho TCP/UDP); a camada de Rede encapsula esse segmento em um pacote (cabeçalho IP); a camada de Enlace encapsula o pacote em um quadro (cabeçalho/trailer de enlace). No destino, o processo inverso (desencapsulamento) remove cada cabeçalho à medida que os dados sobem pelas camadas.",
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = modelos,
            Enunciado = "Qual é o nome da unidade de dados (PDU) trocada na camada de Rede do modelo OSI?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "Pacote",
        });

        // ---------------- Camadas Física e de Enlace ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = enlaceFisica,
            Enunciado = "Qual dispositivo de rede opera principalmente na camada de Enlace, encaminhando quadros com base no endereço MAC de destino?",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Hub" },
                new() { Letra = 'B', Texto = "Switch" },
                new() { Letra = 'C', Texto = "Roteador" },
                new() { Letra = 'D', Texto = "Gateway de aplicação" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = enlaceFisica,
            Enunciado = "Um endereço MAC é, por convenção de fabricação, único para cada interface de rede e possui 48 bits.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = enlaceFisica,
            Enunciado = "Um hub opera na camada de Enlace e segmenta os domínios de colisão da rede, ao contrário do switch.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = enlaceFisica,
            Enunciado = "Qual é o nome da técnica de controle de acesso ao meio usada nas redes Ethernet tradicionais para detectar colisões?",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "CSMA/CD",
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = enlaceFisica,
            Enunciado = "Explique a diferença entre um domínio de colisão e um domínio de broadcast.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Domínio de colisão é o conjunto de dispositivos onde uma transmissão pode colidir com outra transmissão simultânea no mesmo meio físico compartilhado — switches segmentam domínios de colisão (cada porta é seu próprio domínio). Domínio de broadcast é o conjunto de dispositivos que recebem um quadro de broadcast enviado por qualquer um deles — switches, por padrão, NÃO segmentam domínios de broadcast (o broadcast passa por todas as portas), enquanto roteadores segmentam domínios de broadcast.",
        });

        // ---------------- Camada de Rede, IP e Roteamento ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = redeRoteamento,
            Enunciado = "Um endereço IPv4 é formado por quantos bits?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "16 bits" },
                new() { Letra = 'B', Texto = "32 bits" },
                new() { Letra = 'C', Texto = "64 bits" },
                new() { Letra = 'D', Texto = "128 bits" },
            },
        });
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = redeRoteamento,
            Enunciado = "Um endereço IPv6 é formado por quantos bits?",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = 'D',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "32 bits" },
                new() { Letra = 'B', Texto = "48 bits" },
                new() { Letra = 'C', Texto = "64 bits" },
                new() { Letra = 'D', Texto = "128 bits" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = redeRoteamento,
            Enunciado = "O protocolo ARP é responsável por traduzir um endereço IP em um endereço MAC correspondente, dentro de uma mesma rede local.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = redeRoteamento,
            Enunciado = "Endereços IP privados (como os das faixas 10.0.0.0/8 e 192.168.0.0/16) podem ser roteados diretamente pela Internet pública, sem NAT.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = redeRoteamento,
            Enunciado = "Qual é a sigla da técnica que permite que vários dispositivos de uma rede privada compartilhem um único endereço IP público para acessar a Internet?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "NAT",
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = redeRoteamento,
            Enunciado = "Associe cada protocolo de roteamento à sua classificação principal.",
            Dificuldade = Dificuldade.Dificil,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "RIP", Correspondente = "Protocolo de vetor de distância (interno)" },
                new() { Ordem = 1, Termo = "OSPF", Correspondente = "Protocolo de estado de enlace (interno)" },
                new() { Ordem = 2, Termo = "BGP", Correspondente = "Protocolo de roteamento externo (entre sistemas autônomos)" },
            },
        });

        // Sub-rede: hosts utilizáveis = 2^n - 2, onde n = bits de host (32 - prefixo CIDR).
        var subRedeVariantes = new (string RedeExemplo, int PrefixoCidr)[]
        {
            ("192.168.1.0/30", 30), ("10.0.0.0/29", 29), ("172.16.5.0/28", 28), ("192.168.10.0/27", 27),
            ("10.10.0.0/26", 26), ("172.20.0.0/25", 25), ("192.168.0.0/24", 24), ("10.1.1.0/30", 30),
            ("192.168.100.0/28", 28), ("172.31.0.0/27", 27), ("10.20.0.0/29", 29), ("192.168.50.0/25", 25),
        };
        for (var i = 0; i < subRedeVariantes.Length; i++)
        {
            var (rede, prefixo) = subRedeVariantes[i];
            var bitsHost = 32 - prefixo;
            var hostsUtilizaveis = (int)Math.Pow(2, bitsHost) - 2;
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = redeRoteamento,
                Enunciado = $"Considere a sub-rede {rede}. Quantos endereços de host UTILIZÁVEIS (excluindo endereço de rede e de broadcast) essa sub-rede possui?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = hostsUtilizaveis,
                Tolerancia = 0m,
            });
        }

        // Conversão decimal -> binário de um octeto IP (0-255), calculada em tempo de
        // execução (Convert.ToString), então o gabarito é sempre exatamente certo.
        var octetosDecimais = new int[] { 192, 168, 10, 255, 128, 1, 64, 32, 200, 8, 254, 16 };
        for (var i = 0; i < octetosDecimais.Length; i++)
        {
            var decimalOcteto = octetosDecimais[i];
            var binario = Convert.ToString(decimalOcteto, 2).PadLeft(8, '0');
            candidatas.Add(new QuestaoRespostaBreve
            {
                Assunto = redeRoteamento,
                Enunciado = $"Converta o número decimal {decimalOcteto} (um octeto de endereço IPv4) para sua representação binária de 8 bits.",
                Dificuldade = (Dificuldade)((i + 1) % 3),
                RespostaEsperada = binario,
            });
        }

        // ---------------- Camada de Transporte (TCP/UDP) ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = transporte,
            Enunciado = "Qual protocolo de transporte oferece entrega confiável, orientada a conexão, com controle de fluxo e retransmissão de pacotes perdidos?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "TCP" },
                new() { Letra = 'B', Texto = "UDP" },
                new() { Letra = 'C', Texto = "ICMP" },
                new() { Letra = 'D', Texto = "ARP" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = transporte,
            Enunciado = "O UDP é geralmente preferido em aplicações de streaming de vídeo/áudio e jogos online em tempo real, por ter menor overhead e não exigir confirmação de recebimento.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = transporte,
            Enunciado = "O TCP estabelece uma conexão através de um processo conhecido como \"three-way handshake\" (SYN, SYN-ACK, ACK).",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = transporte,
            Enunciado = "Compare o TCP e o UDP quanto à confiabilidade e ao desempenho, e cite um exemplo de aplicação típica para cada um.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "TCP é orientado a conexão, confiável (garante entrega e ordem dos dados via confirmações e retransmissões) e possui controle de fluxo/congestionamento, mas com maior overhead e latência — usado, por exemplo, em navegação web (HTTP/HTTPS) e transferência de arquivos (FTP), onde a integridade dos dados é essencial. UDP é não orientado a conexão, não garante entrega nem ordem, mas tem baixo overhead e latência menor — usado, por exemplo, em streaming de vídeo/áudio em tempo real e jogos online, onde a velocidade importa mais do que a retransmissão de pacotes perdidos.",
        });
        candidatas.Add(new QuestaoNumerica
        {
            Assunto = transporte,
            Enunciado = "Qual é o número máximo teórico de portas TCP (ou UDP) distintas em um único host, considerando que o número de porta é representado em 16 bits (de 0 a 65535)?",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = 65536,
            Tolerancia = 0m,
        });

        // ---------------- Camada de Aplicação e Protocolos ----------------
        var portasConhecidas = new (string Protocolo, int Porta)[]
        {
            ("HTTP", 80), ("HTTPS", 443), ("FTP (controle)", 21), ("SSH", 22), ("Telnet", 23),
            ("SMTP", 25), ("DNS", 53), ("POP3", 110), ("IMAP", 143), ("RDP", 3389),
        };
        for (var i = 0; i < portasConhecidas.Length; i++)
        {
            var (protocolo, porta) = portasConhecidas[i];
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = aplicacao,
                Enunciado = $"Qual é o número da porta padrão (bem conhecida) usada pelo protocolo {protocolo}?",
                Dificuldade = (Dificuldade)(i % 3),
                RespostaEsperada = porta,
                Tolerancia = 0m,
            });
        }
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = aplicacao,
            Enunciado = "Qual protocolo é responsável por traduzir nomes de domínio (ex.: www.exemplo.com) em endereços IP?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'A',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "DNS" },
                new() { Letra = 'B', Texto = "DHCP" },
                new() { Letra = 'C', Texto = "SNMP" },
                new() { Letra = 'D', Texto = "NTP" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = aplicacao,
            Enunciado = "O protocolo DHCP é usado para atribuir automaticamente endereços IP (e outras configurações de rede) aos dispositivos de uma rede.",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = aplicacao,
            Enunciado = "Qual é a sigla do protocolo, baseado em TCP, usado para acesso remoto seguro (criptografado) a um servidor via linha de comando?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "SSH",
        });

        // Tempo de transmissão: t (s) = (tamanho em MB * 8) / taxa (Mbps).
        var transmissaoVariantes = new (int TamanhoMb, int TaxaMbps)[]
        {
            (10, 10), (100, 20), (50, 25), (20, 4), (200, 40), (5, 5), (300, 50), (16, 8), (60, 12), (400, 100), (9, 3), (250, 25),
        };
        for (var i = 0; i < transmissaoVariantes.Length; i++)
        {
            var (tamanhoMb, taxaMbps) = transmissaoVariantes[i];
            var tempoSegundos = tamanhoMb * 8 / taxaMbps;
            candidatas.Add(new QuestaoNumerica
            {
                Assunto = aplicacao,
                Enunciado = $"Um arquivo de {tamanhoMb} MB precisa ser transferido por uma conexão com taxa constante de {taxaMbps} Mbps (considere 1 byte = 8 bits, sem overhead de protocolo). Quanto tempo, em segundos, leva a transferência?",
                Dificuldade = (Dificuldade)((i + 2) % 3),
                RespostaEsperada = tempoSegundos,
                Tolerancia = 0m,
            });
        }

        // ---------------- Segurança de Redes e Redes Sem Fio ----------------
        candidatas.Add(new QuestaoMultiplaEscolha
        {
            Assunto = segurancaRedes,
            Enunciado = "Qual dispositivo/software tem como principal função filtrar o tráfego de rede com base em regras predefinidas, bloqueando acessos não autorizados?",
            Dificuldade = Dificuldade.Facil,
            RespostaCorreta = 'B',
            Alternativas = new()
            {
                new() { Letra = 'A', Texto = "Switch" },
                new() { Letra = 'B', Texto = "Firewall" },
                new() { Letra = 'C', Texto = "Hub" },
                new() { Letra = 'D', Texto = "Access Point" },
            },
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = segurancaRedes,
            Enunciado = "Uma VPN (Virtual Private Network) cria um túnel criptografado sobre uma rede pública, permitindo que o tráfego trafegue com confidencialidade entre dois pontos.",
            Dificuldade = Dificuldade.Media,
            RespostaCorreta = true,
        });
        candidatas.Add(new QuestaoCertoErrado
        {
            Assunto = segurancaRedes,
            Enunciado = "O protocolo WEP é atualmente considerado o padrão de segurança mais robusto para redes Wi-Fi, superior ao WPA2 e ao WPA3.",
            Dificuldade = Dificuldade.Dificil,
            RespostaCorreta = false,
        });
        candidatas.Add(new QuestaoRespostaBreve
        {
            Assunto = segurancaRedes,
            Enunciado = "Qual é a frequência de operação (em GHz) mais tradicional/antiga usada por redes Wi-Fi (padrão 802.11b/g), sujeita a mais interferência de outros dispositivos domésticos?",
            Dificuldade = Dificuldade.Media,
            RespostaEsperada = "2,4 GHz",
        });
        candidatas.Add(new QuestaoAssociacao
        {
            Assunto = segurancaRedes,
            Enunciado = "Associe cada mecanismo de segurança à sua principal função.",
            Dificuldade = Dificuldade.Dificil,
            Pares = new()
            {
                new() { Ordem = 0, Termo = "Firewall", Correspondente = "Filtra tráfego com base em regras (portas, IPs, protocolos)" },
                new() { Ordem = 1, Termo = "VPN", Correspondente = "Cria túnel criptografado sobre rede pública" },
                new() { Ordem = 2, Termo = "IDS/IPS", Correspondente = "Detecta (e opcionalmente bloqueia) tráfego malicioso/anômalo" },
                new() { Ordem = 3, Termo = "NAT", Correspondente = "Traduz endereços privados para um endereço público compartilhado" },
            },
        });
        candidatas.Add(new QuestaoDiscursiva
        {
            Assunto = segurancaRedes,
            Enunciado = "Explique o que é um ataque de negação de serviço distribuído (DDoS) e por que ele é mais difícil de mitigar do que um ataque de negação de serviço (DoS) simples.",
            Dificuldade = Dificuldade.Dificil,
            RespostaEsperada = "Um ataque DDoS (Distributed Denial of Service) sobrecarrega um serviço ou infraestrutura com um volume enorme de requisições, tornando-o indisponível para usuários legítimos — a diferença para um DoS simples é que o tráfego malicioso vem de MUITAS origens distintas (frequentemente uma rede de dispositivos comprometidos, uma \"botnet\"), em vez de uma única origem. Isso torna a mitigação mais difícil porque não é possível simplesmente bloquear um único IP de origem: o tráfego malicioso se mistura ao tráfego legítimo vindo de milhares de endereços diferentes e distintos geograficamente, exigindo defesas mais sofisticadas (ex.: análise de padrão de tráfego, serviços de mitigação na borda da rede/CDN).",
        });

        await DbSeeder.AplicarTipoEInserirNovasAsync(db, candidatas, criadoPorId);
    }
}
