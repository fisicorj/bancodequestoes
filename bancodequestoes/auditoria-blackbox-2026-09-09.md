# Auditoria Blackbox — BancoQuestoes

Data: 09/09/2026
Escopo: aplicação completa (Blazor Server .NET 10 + EF Core + PostgreSQL), leitura de código apenas, sem alterações. Contexto considerado em todas as severidades: uso local/rede local por um professor (eventualmente pequeno grupo), não um SaaS público multi-tenant.

Quatro frentes cobertas: segurança, arquitetura/qualidade, dados/performance, completude de features/UX.

---

## Achados prioritários (ação recomendada primeiro)

### 1. [ALTO — perda de dados] `Questao.AssuntoId` e `Assunto.DisciplinaId` sem `OnDelete` configurado → cascade não intencional

Nenhum dos dois FKs tem `OnDelete` explícito em `ApplicationDbContext.OnModelCreating`. A convenção do EF Core cai em `Cascade` (confirmado em `Migrations/20260829202316_InitialCreate.cs:192-197,250-255`). Isso significa que **excluir uma Disciplina apaga em cascata todos os Assuntos e todas as Questões vinculadas**, a menos que alguma questão já esteja usada em uma Prova (aí o `Restrict` em `ProvaQuestao.QuestaoId` barra tudo com uma exceção).

Isso contradiz o próprio código: `Services/DisciplinaService.cs:134-146` espera que o banco recuse excluir um Assunto com Questões vinculadas — mas isso só acontece se essas questões estiverem em provas. Sem isso, a exclusão simplesmente sucede e apaga o conteúdo. A UI (`AssuntoList.razor:139`) só mostra um `confirm()` genérico, sem avisar quantas questões seriam apagadas.

**Recomendação**: mudar as duas FKs para `DeleteBehavior.Restrict` em `OnModelCreating` + migration, mesmo padrão já usado em `ProvaQuestao`/`ItemMatrizReferencia`. Ajustar a UI de exclusão de Assunto/Disciplina para mostrar a contagem de Questões dependentes antes do `confirm()`.

### 2. [MÉDIO — segurança] Instituição sem restrição de papel

`InstituicaoList.razor`/`InstituicaoForm.razor` só exigem `[Authorize]`, sem `Roles="Admin"` — diferente de todas as demais telas administrativas (`Admin/UsuarioForm.razor`, `Admin/ConfiguracaoIaPage.razor`). Qualquer Professor autenticado pode editar/excluir qualquer instituição, inclusive dados usados no cabeçalho de provas de outros professores. `Services/InstituicaoService.cs` também não faz nenhuma checagem de propriedade internamente.

**Recomendação**: restringir as duas telas a `[Authorize(Roles = "Admin")]`.

### 3. [MÉDIO — segurança] XSS armazenado via duas rotas

- Links Markdown com esquema `javascript:` não são filtrados pelo `DisableHtml()` do Markdig (que só bloqueia HTML bruto, não sintaxe de link normal). Um enunciado com `[clique aqui](javascript:...)` executa ao ser clicado.
- Upload de imagem de questão/logo confia no `Content-Type` declarado pelo navegador sem checar assinatura (magic bytes), e esse valor é reexposto cru como header HTTP em `/questoes/imagem/{id}` e `/instituicoes/logo/{id}` (`Program.cs:151,160`), sem `X-Content-Type-Options: nosniff`.

**Recomendação**: customizar o renderer de link do Markdig para permitir só `http:`/`https:`/`mailto:`; validar magic bytes de imagem e/ou fixar `Content-Type` de resposta via allowlist; adicionar `nosniff` globalmente.

### 4. [MÉDIO — segurança] `/instituicoes/logo/{id}` sem checagem de visibilidade

Único endpoint de recurso por id que não replica o padrão `VisivelPara` usado consistentemente em `/questoes/imagem/{id}` e nos exports de prova. Qualquer usuário autenticado baixa a logo de qualquer instituição adivinhando o id. Impacto baixo (não é dado sensível), mas é a única inconsistência nesse padrão bem estabelecido no resto do app.

### 5. [MÉDIO — segurança] Autocadastro público promove direto a "Professor"

`Register.razor` é uma página pública que cria conta com `EmailConfirmed = true` e já atribui o papel "Professor" automaticamente, sem aprovação de Admin. Em rede local isso costuma ser intencional (facilita o próprio uso), mas qualquer pessoa com acesso à rede vira "Professor" (pode criar/editar questões e provas). Vale confirmar se é o comportamento desejado ou se o autocadastro deveria ser desligado agora que há um fluxo manual de criação de usuário em `Admin/UsuarioForm.razor`.

### 6. [ALTO — manutenção] `Services/SugestaoIaService.cs` (650 linhas) já passou do ponto de dividir

Confirma a preocupação já levantada pelo usuário. 8 métodos, cada um repetindo o mesmo esqueleto (config → prompt → chamada Ollama → DTO → mapeamento). Proposta concreta de split em `partial class` por feature de IA (metadados, revisão de extração, retranscrição, geração, explicação, resposta discursiva) — cada arquivo resultante fica abaixo de 150 linhas. É um corte mecânico (mover blocos inteiros), baixo risco.

### 7. [ALTO — cobertura de teste] Zero teste em exportação DOCX/PDF e nos parsers Aiken/GIFT

`Exportacao/ProvaDocxExporter.cs` (883 linhas), `ProvaPdfExporter.cs` (770 linhas) e os parsers/exportadores Aiken/GIFT não têm nenhum teste — é o código que gera o documento que o professor efetivamente imprime/entrega. Uma regressão de formatação (gabarito errado, numeração duplicada) só apareceria abrindo o arquivo manualmente. Em contraste, o importador ENADE tem cobertura completa (1013 linhas de teste). Recomenda-se pelo menos testes de "smoke" (gerar documento mínimo, verificar que não lança exceção e contém o texto esperado) antes de qualquer refactor nesses arquivos.

---

## Segurança

Resumo completo por severidade (detalhe de cada item nas seções acima e abaixo):

**Crítico**: nenhum encontrado.

**Médio**: Instituição sem restrição de papel (#2); XSS via link `javascript:` e upload de imagem sem validação de assinatura (#3); `/instituicoes/logo/{id}` sem checagem de visibilidade (#4); autocadastro promove a Professor sem aprovação (#5).

**Baixo**:
- `ProvaService.ExcluirAsync`/`DuplicarAsync` não checam `CriadoPorId` no Service (hoje mitigado pela UI, mas frágil — um novo caminho de rota poderia expor isso).
- Ausência de `.gitignore`/repositório git antes de um eventual versionamento futuro dos `.bat` com senha do Postgres em texto plano.
- Login com `lockoutOnFailure: false` — sem proteção de força bruta, apesar do código tratar `IsLockedOut` (código morto que sugere uma proteção que não existe de fato).
- `RespostaCorretaIndex` fora do intervalo é silenciosamente corrigido para 0 em vez de rejeitado; nenhuma validação server-side de mínimo de alternativas em múltipla escolha.

**Observação** (não é bug, é nota): política de senha frouxa é intencional e já documentada; API key de IA em texto plano no banco é aceitável no cenário; segredos não versionados corretamente; `UglyToad.PdfPig 1.7.0-custom-5` tem sufixo de versão atípico mas é documentado no próprio `.csproj` como decisão consciente (não é fork suspeito, é a versão real mais recente no NuGet).

**Pontos fortes confirmados**: autorização por dono de recurso (`VisivelPara`, `CriadoPorId`) é aplicada consistentemente em quase todos os pontos de acesso a Questão/Prova; nenhuma injeção de SQL (zero uso de `FromSqlRaw`/interpolação); Markdown bloqueia HTML bruto; parsers de importação sem padrão de ReDoS; uploads tratados em memória sem risco de path traversal.

---

## Arquitetura e qualidade de código

- **Maior arquivo do projeto**: `ProvaForm.razor` (1429 linhas, 1188 no `@code`, 9 serviços injetados). Não é urgente dividir agora, mas se crescer mais deveria virar markup fino + classe de orquestração separada.
- **Duplicação de padrão "caixa de IA"** (botão + spinner + erro + preview + "usar"): confirmado duplicado 9 vezes em 5 arquivos, incluindo a mesma mensagem de erro copiada quase literalmente em 6 lugares. Vale extrair um componente genérico `IaSugestaoBox.razor` parametrizado por `Func<Task<T?>>` + `RenderFragment<T>` — próxima feature de IA vira ~15 linhas em vez de ~40 repetidas.
- **Anti-padrão de corrida de DbContext entre componentes-irmãos** (o bug corrigido nesta sessão em `ExplicacaoEditor`/`SugestaoIaCard`/`DiscursivaEditor`): busca extensiva não encontrou nenhuma outra ocorrência — a correção foi bem generalizada, nenhuma ação adicional necessária.
- **Migrations**: 28-30, ritmo alto (9 dias) mas qualidade acima da média — comentários de causa-raiz, gates defensivos (`RAISE EXCEPTION` antes de dropar coluna com dado não migrado), backfills idempotentes. Nenhum `DropColumn` perigoso em `Up()`. Duas migrations do mesmo dia (config de visão IA) poderiam ter sido uma só, mas sem custo real.
- **Tratamento de erro**: padrão "IA sempre retorna null em falha" é 100% consistente. Fora do contexto de IA, há ~10 `catch (Exception)` silenciosos em fluxos de upload/parsing (duplicar questão/prova, importar PDF/CSV/XLSX) **sem nenhum `ILogger`** — nenhum componente Razor injeta logger (só as telas de login/registro). Contraste: `EnadeProvaParser.cs` já loga causa raiz corretamente nesses mesmos cenários. Recomenda-se injetar `ILogger<T>` nesses ~7 componentes — mudança mecânica, baixo risco, ganho real de observabilidade na primeira vez que algo quebrar em uso real.
- **Testes**: 106 `[Fact]`, boa cobertura em integridade curricular/escopo e no importador ENADE. Gaps: exportação DOCX/PDF, parsers Aiken/GIFT, e `QuestaoService` isolado (cobertura hoje só incidental via outros testes).
- **Comentários e convenções**: cultura forte e consistente de comentários "por quê" em todo o código (não só em partes recentes), sem TODOs esquecidos, sem código morto aparente, nomenclatura PT/EN consistente com o padrão do domínio.

---

## Integridade de dados e performance

- **Achado principal**: FKs de Questão↔Assunto↔Disciplina sem `OnDelete` explícito (detalhado no item 1 dos achados prioritários) — é o único problema de dados com risco real de perda irreversível.
- **Restante do modelo**: bem cuidado. `Prova.MatrizReferenciaId`, `Curso.AreaCursoId`, `Turma.CursoId/DisciplinaId`, `CursoDisciplina` — todos com `OnDelete` explícito e correto (Restrict onde faz sentido preservar histórico/integridade, SetNull onde o vínculo é opcional). Índices em FKs existem por convenção do EF; `MatrizReferencia`/`ItemMatrizReferencia` têm índices adicionais bem justificados.
- **N+1**: não encontrado nenhum caso problemático real. Listagens e o pool de candidatas do gerador de provas usam `Include`+`AsSplitQuery` ou carregamento em lote via `ToLookup` corretamente.
- **Volume de dados por query**: `.Include(q => q.Imagens)` carrega o `byte[]` completo da imagem mesmo em listagem/grade e no pool do gerador de provas — baixo impacto no volume atual, mas é a query que mais cresce com o tempo; vale separar projeção leve (metadados) de carregamento completo quando o banco de imagens crescer.
- **Migrations**: nenhum `DropColumn`/`AlterColumn` perigoso encontrado; `MigrateAsync()` roda antes de qualquer outro acesso ao banco, idempotente, adequado ao cenário single-instance.
- **Transações**: import em lote (`ImportacaoService.ImportarAsync`) usa transação explícita corretamente. Achado menor: `QuestaoService.CriarAsync`/`AtualizarAsync` fazem dois `SaveChangesAsync()` sequenciais sem transação (resolução de token de imagem pendente) — janela de inconsistência estreita, baixo risco, mas tecnicamente não atômico.
- **Geração de prova**: motor em memória, O(n log n), sem idas repetidas ao banco. Tratamento de "questões insuficientes" é exemplar — aviso textual detalhado por Assunto/Disciplina/Item, nunca falha silenciosa. Melhor componente de engenharia do projeto.
- **Concorrência**: nenhum `Task.WhenAll` no código; nenhum componente Razor injeta `ApplicationDbContext` diretamente (tudo via Services `Scoped`). Nenhum novo ponto de risco além do já corrigido nesta sessão.

---

## Completude de features e UX

**Gaps importantes** (afetam uso real do professor no dia a dia):

1. **Sem exclusão em massa** em nenhuma lista — `QuestaoList.razor` já tem checkbox de seleção, mas só alimenta exportação, não exclusão.
2. **Histórico de questão é só leitura, sem reverter** — o diff de `Enunciado` nem guarda o valor antigo, só sinaliza que mudou; reverter exigiria versionamento por snapshot completo, não a estrutura atual.
3. **Sem backup/restore do banco inteiro** pela aplicação — único caminho hoje é backup direto do Postgres fora do sistema. Dado o cenário (uso local, single professor), um botão simples de exportar tudo em JSON ou disparar `pg_dump` seria valioso.
4. **Empty states inconsistentes** — `QuestaoList.razor` tem ícone + texto orientador + CTA duplo; `DisciplinaList`, `AssuntoList`, `CursoList`, `InstituicaoList`, `TurmaList`, `ProvaList` só mostram uma frase neutra sem ação, mesmo sendo justamente as primeiras telas que um professor novo visita.
5. **Sem numeração de página no DOCX nem no PDF exportado** — para provas impressas de várias páginas, não há como confirmar se uma folha se perdeu. No PDF (QuestPDF) é uma linha de código; no DOCX exige campos `PAGE`/`NUMPAGES` no rodapé.
6. **Sem exportação em lote de várias provas** — `ProvaList.razor` não tem seleção múltipla como `QuestaoList.razor` tem para Aiken/GIFT; baixar 5 provas exige 10 cliques.
7. **Sem checklist de "primeiros passos"** — `Home.razor` mostra os mesmos atalhos para um professor com banco vazio ou cheio, sem indicar por onde começar (Instituição → Curso → Disciplina → Assunto → Questão → Prova). Os formulários individuais já avisam pré-requisito faltante ao tentar criar algo diretamente, mas a tela inicial não guia esse caminho.

**Melhorias desejáveis**: nenhuma lista tem ordenação clicável por coluna; "Duplicar" existe só para Questão e Prova (Turma, que se recria a cada semestre, seria o próximo candidato natural).

**Pontos fortes confirmados**: geração de prova trata muito bem o caso de questões insuficientes (avisos claros, diagnóstico prévio, substituição pontual de uma questão sem regenerar tudo); gabarito comentado exportável separado do enunciado; cabeçalho de prova já configurável por instituição, incluindo campo para nome do aluno; fluxo de importação ENADE é deliberadamente "parser determinístico + revisão manual sempre esperada", não uma lacuna — já é o comportamento por design.

---

## Resumo executivo

Nenhum problema crítico de segurança ou de dados que exija ação imediata/urgente. O achado de maior risco real é a exclusão em cascata não intencional de Questões ao apagar Assunto/Disciplina (perda de dado irreversível). Os demais achados de segurança são de severidade média, coerentes com o cenário de uso local/pequeno grupo (não exigem endurecimento de nível SaaS público). A base de código está bem cuidada — migrations, tratamento de erro de IA, e o motor de geração de prova são pontos fortes claros — com duas áreas concretas de dívida técnica (o serviço de IA a dividir, e a ausência de teste na exportação de documentos) e um conjunto de gaps de UX que, resolvidos, tornariam o uso diário mais fluido sem exigir mudança de arquitetura.
