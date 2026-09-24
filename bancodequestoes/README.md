# TaxProva

Plataforma de avaliação acadêmica: banco de questões, geração e aplicação de provas (impressas ou online), correção automática — inclusive por leitura de cartão resposta via foto — e alinhamento curricular (ENADE/DCN), tudo num só sistema.

O nome vem da junção de **taxonomia** (classificação de questões por disciplina, nível cognitivo — Taxonomia de Bloom — e competências curriculares) com **prova** (o núcleo do sistema: criar, aplicar e corrigir avaliações).

> Projeto construído para uso em instituições de ensino superior brasileiras, com todo o domínio (modelos, telas, mensagens) em português.

## Sumário

- [Principais funcionalidades](#principais-funcionalidades)
- [Tecnologias](#tecnologias)
- [Como rodar localmente](#como-rodar-localmente)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Testes](#testes)
- [Licença](#licença)

## Principais funcionalidades

**Banco de questões**
- Múltipla escolha, certo/errado, discursiva, resposta breve, numérica, associação de colunas e lacunas.
- Enunciados em Markdown + LaTeX (fórmulas matemáticas), com imagens posicionáveis.
- Metadados: Taxonomia de Bloom (com sugestão automática), origem (ENADE/autoral), tags, disciplina/assunto.
- Importação em lote via GIFT e Aiken; importação de provas ENADE completas a partir do PDF oficial (com parser de gabarito e padrão de resposta).
- Histórico/auditoria de alterações e contador de uso de cada questão.

**Provas**
- Geração automática por blueprint (distribuição por disciplina, tipo de questão, nível de Bloom, dificuldade) com diagnóstico de viabilidade antes de gerar.
- Escopo por disciplina, multidisciplinar ou por curso inteiro.
- Versões embaralhadas com gabarito e gabarito comentado.
- Exportação em DOCX e PDF, com cabeçalho de instituição/logo.

**Aplicação e correção**
- **Prova online**: aluno responde por um link com código individual, com correção automática das questões objetivas.
- **Cartão resposta (impresso)**: gera um PDF com QR Code por aluno; o professor fotografa os cartões preenchidos e o sistema lê a foto (localização de marcadores, correção de perspectiva e leitura de bolha) — sempre com uma tela de conferência antes de confirmar a nota.

**Alinhamento curricular**
- Matrizes de referência (ENADE, DCN ou personalizadas) vinculadas a questões, com dashboard de cobertura curricular.
- Importação de matrizes via CSV/XLSX/JSON.

**Gestão acadêmica**
- Instituições, cursos, turmas, alunos e matrícula (com importação via CSV).
- Perfis de professor e administração de usuários.

**Inteligência artificial (opcional)**
- Sugestão de metadados (Bloom, tags, alinhamento curricular) e geração de questões novas via LLM local (Ollama) ou em nuvem.

## Tecnologias

- [.NET 10](https://dotnet.microsoft.com/) / ASP.NET Core com [Blazor](https://learn.microsoft.com/aspnet/core/blazor/) (Server-side)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/) + [PostgreSQL](https://www.postgresql.org/) (via Npgsql)
- ASP.NET Core Identity (autenticação e autorização)
- [PDFsharp / MigraDoc](https://github.com/empira/PDFsharp) e [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) — exportação em PDF e DOCX
- [UglyToad.PdfPig](https://github.com/UglyToad/PdfPig) e [PDFtoImage](https://github.com/sungaila/PDFtoImage) — leitura/renderização de PDF (importador ENADE)
- [QRCoder](https://github.com/codebude/QRCoder) e [ZXing.Net](https://github.com/micjahn/ZXing.Net) — geração e leitura de QR Code (cartão resposta)
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) — processamento de imagem (correção de perspectiva e leitura de bolha)
- [Markdig](https://github.com/xoofx/markdig) — enunciados em Markdown
- xUnit — testes automatizados

## Como rodar localmente

### Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) rodando localmente (ou acessível por rede)
- (Opcional) [Ollama](https://ollama.com/) para usar a geração de questões por IA local

### Passos

1. Clone o repositório e entre na pasta do projeto:

   ```bash
   git clone https://github.com/<seu-usuario>/taxprova.git
   cd taxprova
   ```

2. Configure a connection string do banco. Use o `dotnet user-secrets` (recomendado, evita commitar credenciais) ou o `appsettings.Development.json`:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=taxprova;Username=postgres;Password=SUA_SENHA"
   ```

3. Aplique as migrations pra criar o esquema do banco:

   ```bash
   dotnet ef database update
   ```

4. Rode o projeto:

   ```bash
   dotnet run
   ```

5. Acesse `https://localhost:5001` (ou a porta exibida no terminal) e crie sua conta de professor pela tela de registro.

### Configuração opcional de IA

A geração/sugestão de questões por IA é opcional e desativada automaticamente se não configurada. Pra habilitar com Ollama local, ajuste a seção `Ollama` do `appsettings.json` (URL, modelo, timeout) ou configure um provedor em nuvem pela tela `/admin/configuracao-ia` dentro do próprio sistema.

## Estrutura do projeto

```
├── Components/       # Páginas e componentes Blazor (UI)
├── Services/         # Regras de negócio (uma classe por domínio)
├── models/           # Entidades do EF Core
├── Data/             # ApplicationDbContext
├── Migrations/       # Migrations do EF Core
├── Exportacao/        # Geração de DOCX/PDF (provas, gabaritos, cartão resposta)
├── Importacao/        # Parsers de importação (GIFT, Aiken, ENADE, matrizes)
├── CartaoResposta/     # Layout e leitura de imagem do cartão resposta (OMR)
└── bancodequestoes.Tests/  # Testes automatizados (xUnit)
```

## Testes

```bash
cd bancodequestoes.Tests
dotnet test
```

## Licença

Distribuído sob a licença MIT — veja o arquivo [LICENSE](LICENSE) para mais detalhes.
