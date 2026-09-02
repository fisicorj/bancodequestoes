# Banco de Questões

Aplicação web para **gestão de banco de questões, elaboração de avaliações e acompanhamento de cobertura curricular**, desenvolvida com ASP.NET Core Blazor e PostgreSQL.

O projeto foi criado para apoiar professores e instituições no cadastro, organização, classificação, seleção e reutilização de questões, permitindo a montagem manual ou automática de provas e a associação das questões a matrizes de referência como **ENADE, DCN, PPC e matrizes institucionais**.

> **Status:** projeto em desenvolvimento.

---

## Principais funcionalidades

### Banco de questões

- Cadastro, edição e organização de questões.
- Questões de múltipla escolha.
- Questões discursivas.
- Questões de certo ou errado.
- Questões numéricas.
- Questões de resposta breve.
- Questões de lacunas.
- Suporte a imagens.
- Enunciados com Markdown.
- Suporte a fórmulas e expressões matemáticas.
- Organização por disciplina e assunto.
- Tags.
- Classificação por dificuldade.
- Classificação pela Taxonomia de Bloom.
- Indicadores de qualidade.
- Histórico de utilização das questões.

### Importação e exportação

- Importação de questões no formato **AIKEN**.
- Importação de questões no formato **GIFT**.
- Exportação para AIKEN e GIFT.
- Importação de itens de matrizes curriculares.
- Suporte a CSV, XLSX e JSON para dados de matriz, conforme as funcionalidades disponíveis no projeto.

### Geração de provas

- Montagem manual de avaliações.
- Geração automática de provas.
- Distribuição por assunto.
- Distribuição por dificuldade.
- Distribuição por tipo de questão.
- Distribuição pela Taxonomia de Bloom.
- Controle de questões recentemente utilizadas.
- Filtros de qualidade.
- Ordenação das questões.
- Definição de pontuação.
- Geração de variações da mesma prova.

### Exportação de avaliações

- Exportação para **DOCX**.
- Exportação para **PDF**.
- Geração de variações da prova.
- Geração de gabarito comentado.

### Matriz de Referência Curricular

O sistema possui uma estrutura de matrizes versionadas que pode representar diferentes referências acadêmicas:

- ENADE;
- Diretrizes Curriculares Nacionais — DCN;
- Projeto Pedagógico de Curso — PPC;
- Matrizes institucionais;
- Outras referências curriculares.

Uma matriz pode conter itens como:

- Perfil do concluinte;
- Competências;
- Conteúdos;
- Habilidades;
- Objetos de conhecimento.

As questões podem ser associadas a múltiplos itens da matriz, permitindo acompanhar a cobertura curricular do banco de questões e das avaliações.

Exemplo conceitual:

```text
Engenharia de Computação
└── ENADE 2023
    ├── Perfil do Concluinte
    ├── Competências
    │   └── C08 - Implementar e gerenciar a segurança
    │             de sistemas de computação
    └── Conteúdos
        └── CT20 - Segurança de sistemas de computação
```

O repositório também contém uma estrutura de referência para a matriz do **ENADE 2023 de Engenharia de Computação**, baseada na Portaria INEP nº 279/2023.

---

## Cobertura curricular

A aplicação permite analisar a relação entre o banco de questões e as matrizes cadastradas.

Isso possibilita responder perguntas como:

- Quais competências possuem mais questões?
- Quais competências ainda possuem pouca cobertura?
- Quais conteúdos estão sendo avaliados?
- Quais disciplinas contribuem para determinada competência?
- Existem questões de diferentes níveis de dificuldade para uma competência?
- Quais níveis da Taxonomia de Bloom estão representados?

A evolução desse módulo busca transformar o projeto em uma plataforma de:

> **Banco de Questões + Gestão da Avaliação + Cobertura Curricular**

---

## Tecnologias

- **.NET 10**
- **ASP.NET Core**
- **Blazor Interactive Server**
- **Entity Framework Core 10**
- **PostgreSQL**
- **ASP.NET Core Identity**
- **Npgsql**
- **Markdig**
- **Open XML SDK**
- **PDFsharp / MigraDoc**

---

## Estrutura principal do projeto

```text
bancodequestoes/
├── Components/
│   ├── Account/
│   ├── Layout/
│   └── Pages/
│
├── Data/
│   └── DbSeeder.cs
│
├── Exportacao/
│   ├── ProvaDocxExporter.cs
│   ├── ProvaPdfExporter.cs
│   └── ...
│
├── Importacao/
│   ├── AikenParser.cs
│   ├── GiftParser.cs
│   └── ItemMatrizImportParser.cs
│
├── Migrations/
│
├── models/
│   ├── ApplicationDbContext.cs
│   ├── Questao.cs
│   ├── Prova.cs
│   ├── Curso.cs
│   ├── Disciplina.cs
│   ├── MatrizReferencia.cs
│   └── ...
│
├── Services/
│   ├── QuestaoService.cs
│   ├── ProvaService.cs
│   ├── MatrizReferenciaService.cs
│   ├── ImportacaoService.cs
│   └── ...
│
├── wwwroot/
│
├── Program.cs
├── appsettings.json
└── bancodequestoes.csproj
```

---

## Pré-requisitos

Para executar o projeto localmente, instale:

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [PostgreSQL](https://www.postgresql.org/)
- Git
- Visual Studio 2022/2026 compatível com .NET 10, Visual Studio Code ou Rider

Para verificar a versão do .NET instalada:

```bash
dotnet --version
```

---

## Clonando o projeto

```bash
git clone https://github.com/SEU-USUARIO/bancodequestoes.git
cd bancodequestoes
```

Ajuste a URL acima para o endereço real deste repositório.

---

## Configurando o banco PostgreSQL

Crie um banco PostgreSQL para a aplicação.

Exemplo:

```sql
CREATE DATABASE bancoquestoes;
```

A aplicação procura uma connection string chamada:

```text
DefaultConnection
```

### Recomendado: usar User Secrets

Não coloque usuário e senha reais do PostgreSQL no código ou em arquivos enviados ao GitHub.

Na pasta que contém o arquivo `bancodequestoes.csproj`, execute:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=bancoquestoes;Username=postgres;Password=SUA_SENHA"
```

O projeto já possui um `UserSecretsId` configurado no `.csproj`.

Para conferir os secrets configurados:

```bash
dotnet user-secrets list
```

---

## Restaurando as dependências

Na pasta do projeto:

```bash
dotnet restore
```

---

## Criando/atualizando o banco

O projeto utiliza migrations do Entity Framework Core.

Caso ainda não tenha a ferramenta `dotnet-ef` instalada:

```bash
dotnet tool install --global dotnet-ef
```

Se ela já estiver instalada, você pode atualizá-la:

```bash
dotnet tool update --global dotnet-ef
```

Depois execute:

```bash
dotnet ef database update
```

Esse comando aplica as migrations existentes ao PostgreSQL.

> As migrations fazem parte do código-fonte e devem permanecer versionadas no Git.

---

## Executando a aplicação

```bash
dotnet run
```

No ambiente de desenvolvimento, os perfis atuais utilizam, entre outros, os endereços:

```text
https://localhost:7007
http://localhost:5192
```

O endereço efetivo também será exibido no terminal durante a inicialização.

---

## Executando pelo Visual Studio

1. Abra a solução/projeto no Visual Studio.
2. Configure a `DefaultConnection` usando User Secrets.
3. Confirme que o PostgreSQL está disponível.
4. Aplique as migrations.
5. Execute o projeto com `F5` ou `Ctrl+F5`.

---

## Autenticação e autorização

A aplicação utiliza **ASP.NET Core Identity**.

Atualmente estão previstos os papéis:

- `Professor`
- `Admin`

Os papéis são verificados/criados durante a inicialização da aplicação.

O sistema também possui regras relacionadas a:

- propriedade das provas;
- visibilidade das questões;
- instituição;
- cursos;
- acesso a imagens;
- exportação das avaliações.

Essas regras devem continuar sendo revisadas antes de disponibilizar a aplicação em ambiente de produção ou multi-institucional.

---

## Dados iniciais

O projeto possui rotinas de `DbSeeder` executadas durante a inicialização.

Elas podem inserir ou ajustar dados de demonstração, incluindo bancos de questões de algumas disciplinas.

Antes de utilizar a aplicação em produção, revise o conteúdo de:

```text
Data/DbSeeder.cs
```

e confirme quais seeds devem permanecer habilitados.

---

## Segurança

### Nunca envie para o GitHub

- senhas;
- connection strings com credenciais;
- tokens;
- API keys;
- certificados privados;
- arquivos `.env`;
- dumps de banco de dados;
- dados pessoais ou institucionais reais;
- banco local;
- conteúdo de produção.

Use **User Secrets** durante o desenvolvimento e variáveis de ambiente ou um gerenciador de segredos no ambiente de produção.

### Antes do primeiro push

Execute:

```bash
git status
```

e confira cuidadosamente tudo que será enviado.

Também é recomendável verificar:

```bash
git diff --cached
```

antes de cada commit importante.

---

## Primeiro commit

Depois de colocar `.gitignore` e `README.md` na raiz do repositório:

```bash
git init
git add .
git status
git commit -m "Initial commit"
```

Depois conecte o repositório local ao GitHub:

```bash
git branch -M main
git remote add origin https://github.com/SEU-USUARIO/bancodequestoes.git
git push -u origin main
```

---

## Fluxo básico de desenvolvimento

Após novas alterações:

```bash
git status
git add .
git commit -m "Descrição da alteração"
git push
```

Exemplos de mensagens:

```text
feat: adiciona matriz de referência curricular
fix: corrige geração automática de provas
feat: adiciona exportação de gabarito comentado
refactor: reorganiza serviço de questões
docs: atualiza README
```

---

## Próximas evoluções

Entre as evoluções possíveis do projeto estão:

- blueprint curricular na geração de provas;
- distribuição de questões por competência/conteúdo;
- diagnóstico de disponibilidade antes da geração;
- comparação entre cobertura planejada e obtida;
- dashboards institucionais;
- melhoria do isolamento entre instituições;
- importação completa de matrizes oficiais;
- ampliação de relatórios de cobertura;
- classificação assistida de questões.

---

## Licença

Ainda não foi definida uma licença para o projeto.

Se o repositório for público, escolha uma licença compatível com a forma como você deseja permitir o uso, modificação e distribuição do código.

---

## Autor

**Manoel Moraes**

Projeto desenvolvido para apoiar a criação, organização e análise pedagógica de bancos de questões e avaliações acadêmicas.
