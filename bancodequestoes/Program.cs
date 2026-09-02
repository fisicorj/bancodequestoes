using BancoQuestoes.Components.Account;
using BancoQuestoes.Data;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Fonts;
using System.Security.Claims;

// WebApplication.CreateBuilder é o ponto de entrada dos apps ASP.NET Core
// "minimal hosting" (desde .NET 6): "builder" acumula configuração e serviços,
// e no final vira o "app" que efetivamente atende requisições.
var builder = WebApplication.CreateBuilder(args);

// PDFsharp 6.2+ não sabe de onde tirar fontes por padrão (roda em qualquer SO,
// então não presume nada sobre o sistema). Como este app roda no Windows, isso
// deixa ele usar as fontes de C:\Windows\Fonts para as famílias comuns
// (Arial, Times New Roman, Courier New etc.) — precisa rodar antes da primeira
// fonte ser usada, então fica logo no início.
GlobalFontSettings.UseWindowsFontsUnderWindows = true;

// --- Registro de serviços (injeção de dependência) ---
// Tudo que é adicionado com builder.Services.Add... fica disponível para ser
// "injetado" (recebido no construtor) em qualquer componente Blazor, controller
// ou outro serviço, sem você precisar instanciar manualmente com "new".

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// GetConnectionString("DefaultConnection") lê a string de conexão do
// appsettings.json (seção "ConnectionStrings"). Manter a connection string
// fora do código é o padrão: facilita trocar de ambiente (dev/produção)
// sem recompilar.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// AddIdentity configura todo o sistema de login/senha/roles.
// AddRoles<IdentityRole> habilita os papéis (Professor, Admin) que você
// vai usar para autorização. AddEntityFrameworkStores liga o Identity
// ao ApplicationDbContext, então os usuários ficam nas mesmas tabelas
// do resto do sistema (AspNetUsers, AspNetRoles, dentro do Postgres).
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Regras de senha mais brandas fazem sentido para um sistema de uso
    // próprio/pequeno grupo; ajuste para produção real com mais gente.
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Necessário para os componentes Blazor conseguirem saber "quem está logado"
// (via AuthenticationStateProvider) e para os atributos [Authorize] funcionarem.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Usado pelas páginas de Login/Registro (renderizadas estaticamente) para redirecionar
// depois de autenticar, já que ali um NavigateTo comum é tratado como redirect HTTP real.
builder.Services.AddScoped<IdentityRedirectManager>();

// Camada de Services (em construção incremental — ver Services/DisciplinaService.cs).
// Scoped, igual o ApplicationDbContext: em Blazor Server isso significa "uma
// instância por circuito", então o Service enxerga o mesmo DbContext (com o
// mesmo change tracking) do início ao fim da sessão do usuário na página.
builder.Services.AddScoped<DisciplinaService>();
builder.Services.AddScoped<QuestaoService>();
builder.Services.AddScoped<ProvaService>();
builder.Services.AddScoped<ImportacaoService>();
builder.Services.AddScoped<ExportacaoService>();
builder.Services.AddScoped<EstatisticaService>();
builder.Services.AddScoped<InstituicaoService>();
builder.Services.AddScoped<CursoService>();
builder.Services.AddScoped<AreaCursoService>();
builder.Services.AddScoped<MatrizReferenciaService>();

var app = builder.Build();

// --- Pipeline HTTP (middlewares) ---
// A ordem aqui importa: cada "app.Use..." é uma etapa que a requisição passa
// antes de chegar nos componentes Blazor.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Precisa vir ANTES de MapRazorComponents: primeiro identifica/autentica
// o usuário, depois autoriza o acesso à rota, só então renderiza o componente.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<BancoQuestoes.Components.App>()
    .AddInteractiveServerRenderMode();

// Endpoint de logout: fica fora de um componente Blazor de propósito, porque
// encerrar a sessão exige escrever no cookie de resposta, o que só é possível
// numa requisição HTTP normal (não numa conexão SignalR já aberta).
//
// DisableAntiforgery(): o NavMenu (com o form de logout) fica ora estático ora
// interativo dependendo da página atual, porque o @rendermode é definido por
// página aqui. Isso faz o token antiforgery embutido no form ficar defasado
// entre uma renderização e outra ("meant for a different claims-based user").
// Como sair da conta não é uma ação sensível (só invalida o próprio login),
// desligar a validação aqui é um trade-off aceitável para não travar o logout.
// Serve o conteúdo de uma imagem de questão direto do Postgres. [Authorize]
// sozinho só garante login — não impede um professor de adivinhar/incrementar
// o id na URL (/questoes/imagem/1, /2, /3...) e ver imagem de questão privada
// de outro professor. Por isso o Service aplica a mesma regra VisivelPara
// usada em toda tela que lista questões antes de servir o arquivo.
app.MapGet("/questoes/imagem/{id:int}", async (int id, HttpContext http, QuestaoService questaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var minhaInstituicaoId = await questaoService.ObterInstituicaoDoUsuarioAsync(meuId);
    var imagem = await questaoService.ObterImagemVisivelAsync(id, meuId, minhaInstituicaoId);
    return imagem is null ? Results.NotFound() : Results.File(imagem.Conteudo, imagem.ContentType);
}).RequireAuthorization();

// Serve a logo de uma instituição direto do Postgres.
app.MapGet("/instituicoes/logo/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var instituicao = await db.Instituicoes.FindAsync(id);
    return instituicao?.LogoConteudo is null
        ? Results.NotFound()
        : Results.File(instituicao.LogoConteudo, instituicao.LogoContentType ?? "application/octet-stream");
}).RequireAuthorization();

// Exportação da prova. GET simples: o navegador baixa/abre o arquivo direto ao
// acessar a URL, sem precisar de JavaScript no front-end.
//
// Provas são privadas por professor, então cada endpoint confere se quem está
// pedindo o arquivo foi quem criou a prova — sem isso, bastaria adivinhar/trocar
// o número no final da URL pra baixar a prova de outro professor.
app.MapGet("/provas/{id:int}/exportar.docx", async (int id, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var bytes = ExportacaoService.GerarDocx(prova);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + ".docx");
}).RequireAuthorization();

app.MapGet("/provas/{id:int}/exportar.pdf", async (int id, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var bytes = ExportacaoService.GerarPdf(prova);
    return Results.File(bytes, "application/pdf", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + ".pdf");
}).RequireAuthorization();

// Variações da prova: gera "qtd" versões com questões/alternativas embaralhadas
// (2 a 6, com 2 como padrão) seguidas de uma página de gabarito com a resposta
// certa de cada versão — útil pra dificultar cola em prova impressa.
app.MapGet("/provas/{id:int}/exportar-variacoes.docx", async (int id, int? qtd, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var quantidade = Math.Clamp(qtd ?? 2, 2, 6);
    var bytes = ExportacaoService.GerarVariacoesDocx(prova, quantidade);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + "-variacoes.docx");
}).RequireAuthorization();

app.MapGet("/provas/{id:int}/exportar-variacoes.pdf", async (int id, int? qtd, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var quantidade = Math.Clamp(qtd ?? 2, 2, 6);
    var bytes = ExportacaoService.GerarVariacoesPdf(prova, quantidade);
    return Results.File(bytes, "application/pdf", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + "-variacoes.pdf");
}).RequireAuthorization();

// Gabarito comentado: documento separado da prova em branco — cada questão
// seguida da resposta certa e, se preenchida, da explicação do professor.
// Pensado como material de estudo/revisão pro aluno depois da prova aplicada,
// ou apoio na hora de corrigir — não é pra ser aplicado como prova.
app.MapGet("/provas/{id:int}/gabarito-comentado.docx", async (int id, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var bytes = ExportacaoService.GerarGabaritoComentadoDocx(prova);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + "-gabarito-comentado.docx");
}).RequireAuthorization();

app.MapGet("/provas/{id:int}/gabarito-comentado.pdf", async (int id, HttpContext http, ExportacaoService exportacaoService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await exportacaoService.EhDonoDaProvaAsync(id, meuId))
    {
        return Results.NotFound();
    }
    var prova = await exportacaoService.CarregarAsync(id);
    if (prova is null)
    {
        return Results.NotFound();
    }
    var bytes = ExportacaoService.GerarGabaritoComentadoPdf(prova);
    return Results.File(bytes, "application/pdf", ExportacaoService.NomeArquivoSeguro(prova.Titulo) + "-gabarito-comentado.pdf");
}).RequireAuthorization();

app.MapPost("/Account/Logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();

    // Sempre volta pra tela inicial ao sair, independente de onde o usuário estava.
    return Results.LocalRedirect("/");
}).DisableAntiforgery();

// Troca de senha: fica fora de um componente Blazor pelo mesmo motivo do logout —
// SignInManager.RefreshSignInAsync precisa reescrever o cookie de autenticação com
// o novo "security stamp", e isso só é possível numa requisição HTTP normal (a tela
// de perfil roda em modo interativo, com a conexão SignalR já aberta).
//
// Diferente do form de logout do NavMenu (que aparece em páginas com render mode
// variável, o que deixa o token antiforgery defasado entre uma renderização e
// outra), a tela de perfil é sempre @rendermode InteractiveServer do início ao fim
// — então o <AntiforgeryToken /> embutido no form (PerfilEdicao.razor) fica válido
// e a validação normal do app.UseAntiforgery() pode ficar ligada aqui.
app.MapPost("/Account/TrocarSenha", async (
    HttpContext http,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] string senhaAtual,
    [FromForm] string novaSenha,
    [FromForm] string confirmarSenha) =>
{
    var usuario = await userManager.GetUserAsync(http.User);
    if (usuario is null)
    {
        return Results.LocalRedirect("/perfil");
    }

    if (novaSenha != confirmarSenha)
    {
        return Results.LocalRedirect($"/perfil?senhaErro={Uri.EscapeDataString("A nova senha e a confirmação não coincidem.")}");
    }

    var resultado = await userManager.ChangePasswordAsync(usuario, senhaAtual, novaSenha);
    if (!resultado.Succeeded)
    {
        var erro = string.Join(" ", resultado.Errors.Select(e => e.Description));
        return Results.LocalRedirect($"/perfil?senhaErro={Uri.EscapeDataString(erro)}");
    }

    // Renova o cookie com o novo security stamp, pra não derrubar a sessão atual.
    await signInManager.RefreshSignInAsync(usuario);
    return Results.LocalRedirect("/perfil?senhaOk=true");
}).RequireAuthorization();

// Cria automaticamente os papéis "Professor" e "Admin" se ainda não existirem,
// toda vez que a aplicação sobe. Prático para ambiente de desenvolvimento —
// evita ter que criar isso manualmente no banco.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Professor", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Bancos de questões de teste (Engenharia de Software, Matemática,
    // Introdução à Computação) — cada método só insere o que ainda não existe
    // (por enunciado), então rodar de novo não duplica nada.
    //
    // criadoPorId (primeiro usuário cadastrado) faz as questões nascerem com
    // dono — sem isso, e sem Visibilidade=Compartilhada (setado dentro do
    // DbSeeder), o filtro VisivelPara escondia as questões seedadas de todo
    // mundo, já que "CriadoPorId == meuId" nunca bate com null.
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var criadoPorId = await dbContext.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
    await DbSeeder.RepararQuestoesSemDonoAsync(dbContext, criadoPorId);
    await DbSeeder.SeedEngenhariaDeSoftwareAsync(dbContext, criadoPorId);
    await DbSeeder.SeedMatematicaAsync(dbContext, criadoPorId);
    await DbSeeder.SeedIntroducaoComputacaoAsync(dbContext, criadoPorId);

    // Backfill: dá a tag "Com imagem" pra questão que já tinha imagem antes
    // dessa sincronização existir (daqui pra frente, QuestaoService cuida
    // disso sozinho em cada criação/edição). Idempotente, seguro rodar toda
    // vez que o app sobe.
    var questaoService = scope.ServiceProvider.GetRequiredService<QuestaoService>();
    await questaoService.SincronizarTagsDeImagemEmMassaAsync();
}

app.Run();
