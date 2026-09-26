using BancoQuestoes.Components.Account;
using BancoQuestoes.Data;
using BancoQuestoes.Importacao;
using BancoQuestoes.Models;
using BancoQuestoes.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Fonts;
using System.Runtime.Versioning;
using System.Security.Claims;

// Declarado uma vez pro assembly inteiro (nunca publicado pra browser/Android/iOS) —
// resolve o CA1416 no registro de PaginaEnadeRenderizador (ver a mesma anotação lá)
// sem precisar de pragma; qualquer chamada nova a uma API "platform-specific" coberta
// por esses três SOs passa a ser reconhecida como segura automaticamente.
[assembly: SupportedOSPlatform("windows")]
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]

// "builder" acumula configuração e serviços; no final vira o "app" que atende requisições.
var builder = WebApplication.CreateBuilder(args);

// Fora de Development, o ASP.NET Core desliga por padrão a resolução de
// Static Web Assets (blazor.web.js, *.razor.js dos componentes, o
// @Assets[...] usado em App.razor para fingerprint de CSS/JS) — o
// pressuposto é que em produção você rodou "dotnet publish", que já embute
// tudo isso em wwwroot. Aqui a v1 continua rodando via "dotnet run" direto
// do código-fonte (mesma máquina do dev, sem publish), então sem essa
// chamada os arquivos da UI voltam 404 assim que ASPNETCORE_ENVIRONMENT
// deixa de ser "Development" (ver executar-producao.bat).
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseStaticWebAssets();
}

// PDFsharp 6.2+ não presume o SO; roda antes da 1ª fonte ser usada pra
// habilitar as fontes de C:\Windows\Fonts.
GlobalFontSettings.UseWindowsFontsUnderWindows = true;

// --- Registro de serviços (injeção de dependência) ---

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Connection string fora do código: facilita trocar de ambiente sem recompilar.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// AddIdentity configura login/senha/roles; AddEntityFrameworkStores liga ao
// ApplicationDbContext, então usuários ficam nas mesmas tabelas do Postgres.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Regras mais brandas fazem sentido pra uso próprio/pequeno grupo.
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

// Usado pelas páginas de Login/Registro (estáticas) pra redirecionar depois de
// autenticar, já que ali um NavigateTo comum vira redirect HTTP real.
builder.Services.AddScoped<IdentityRedirectManager>();

// Scoped, igual o ApplicationDbContext: em Blazor Server é "uma instância por
// circuito", então o Service enxerga o mesmo DbContext do início ao fim da sessão.
builder.Services.AddScoped<DisciplinaService>();
builder.Services.AddScoped<QuestaoService>();
builder.Services.AddScoped<QuestaoQueryService>();
builder.Services.AddScoped<QuestaoTagService>();
builder.Services.AddScoped<QuestaoHistoricoService>();
builder.Services.AddScoped<QuestaoImagemService>();
builder.Services.AddScoped<QuestaoCurricularService>();
builder.Services.AddScoped<ProvaService>();
builder.Services.AddScoped<GeradorProvaService>();
builder.Services.AddScoped<ImportacaoService>();
builder.Services.AddScoped<ExportacaoService>();
builder.Services.AddScoped<EstatisticaService>();
builder.Services.AddScoped<InstituicaoService>();
builder.Services.AddScoped<CursoService>();
builder.Services.AddScoped<AreaCursoService>();
builder.Services.AddScoped<MatrizAcessoService>();
builder.Services.AddScoped<ItemMatrizReferenciaService>();
builder.Services.AddScoped<ItemMatrizVinculoService>();
builder.Services.AddScoped<MatrizReferenciaService>();
builder.Services.AddScoped<IImportadorProvaEnadeService, ImportadorProvaEnadeService>();
builder.Services.AddScoped<AlunoService>();
builder.Services.AddScoped<AplicacaoProvaService>();
builder.Services.AddScoped<RespostaProvaOnlineService>();
builder.Services.AddScoped<CartaoRespostaService>();

// Configuração de IA de verdade mora no banco (ConfiguracaoIa); appsettings
// "Ollama" só alimenta os valores iniciais da primeira leitura.
builder.Services.Configure<SugestaoIaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddScoped<ConfiguracaoIaService>();
builder.Services.AddHttpClient<OllamaClient>();
builder.Services.AddHttpClient<SugestaoIaService>();

// Scoped de propósito: precisa sobreviver à navegação no MESMO circuito
// Blazor Server, mas nunca vazar entre usuários/circuitos diferentes.
builder.Services.AddScoped<RascunhoQuestaoIaService>();

// Singleton: PaginaEnadeRenderizador não guarda estado por requisição (só uma
// trava estática global pela thread-safety do PDFium).
builder.Services.AddSingleton<IPaginaPdfRenderizador, PaginaEnadeRenderizador>();

// Achado médio da auditoria: /responder/{codigo} é o ÚNICO endpoint público sem login do
// sistema (o código tem ~1,3 bilhão de combinações, mas nada impedia um script tentar
// milhares por segundo). O limitador é GLOBAL mas só aplica janela de verdade quando o path
// começa com "/responder" — qualquer outra rota cai no "sem-limite" (NoLimiter), então isso
// não afeta o resto do sistema, que já exige login.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        if (!http.Request.Path.StartsWithSegments("/responder"))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("sem-limite");
        }

        var chave = http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(chave, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// --- Pipeline HTTP (middlewares) --- a ordem importa: cada "app.Use..." é
// uma etapa antes de chegar nos componentes Blazor.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRateLimiter();

// Precisa vir ANTES de MapRazorComponents: primeiro identifica/autentica
// o usuário, depois autoriza o acesso à rota, só então renderiza o componente.
app.UseAuthentication();
app.UseAuthorization();

// Endpoint que efetivamente serve os arquivos com fingerprint referenciados
// via "@Assets[...]" (App.razor) e os arquivos do próprio framework Blazor
// (blazor.web.js, ReconnectModal.razor.js) e o CSS isolado dos componentes
// (bancodequestoes.styles.css). Sem isso, esses arquivos voltam 404 fora do
// ambiente Development — o UseStaticFiles() sozinho não é suficiente aqui,
// tanto rodando via "dotnet run" quanto numa build publicada (dotnet publish).
app.MapStaticAssets();

app.MapRazorComponents<BancoQuestoes.Components.App>()
    .AddInteractiveServerRenderMode();

// Fora de um componente Blazor pois encerrar sessão exige escrever no cookie
// (HTTP normal); [Authorize] não barra adivinhar o id, daí a VisivelPara abaixo.
app.MapGet("/questoes/imagem/{id:int}", async (int id, HttpContext http, QuestaoService questaoService, QuestaoImagemService imagemService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var minhaInstituicaoId = await questaoService.ObterInstituicaoDoUsuarioAsync(meuId);
    var imagem = await imagemService.ObterVisivelAsync(id, meuId, minhaInstituicaoId);
    if (imagem is null)
    {
        return Results.NotFound();
    }
    // nosniff: o Content-Type gravado já é validado por assinatura no upload (ValidadorImagem),
    // mas isso reforça que o navegador nunca tente "adivinhar" outro tipo pro conteúdo.
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(imagem.Conteudo, imagem.ContentType);
}).RequireAuthorization();

// Serve a logo de uma instituição direto do Postgres. Sem checagem de "dono" — diferente
// de Questao/Prova, Instituicao é um recurso compartilhado entre todos os professores
// (aparece no cabeçalho de provas de qualquer um), gerenciado só por Admin desde que
// InstituicaoList/InstituicaoForm passaram a exigir Roles="Admin"; qualquer usuário
// autenticado pode ver a logo, só não pode mais criar/editar/excluir instituição.
app.MapGet("/instituicoes/logo/{id:int}", async (int id, HttpContext http, ApplicationDbContext db) =>
{
    var instituicao = await db.Instituicoes.FindAsync(id);
    if (instituicao?.LogoConteudo is null)
    {
        return Results.NotFound();
    }
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(instituicao.LogoConteudo, instituicao.LogoContentType ?? "application/octet-stream");
}).RequireAuthorization();

// GET simples: baixa direto sem JS. Cada endpoint confere se quem pede é quem
// criou a prova, senão bastaria trocar o id na URL.
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

// Gera "qtd" versões embaralhadas (2 a 6, padrão 2) com gabarito por versão —
// útil pra dificultar cola em prova impressa.
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

// Documento separado da prova em branco: cada questão seguida da resposta e,
// se houver, da explicação — material de estudo/apoio, não pra ser aplicado.
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

app.MapGet("/cartoes-resposta/{id:int}/cartoes.pdf", async (int id, HttpContext http, CartaoRespostaService cartaoRespostaService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await cartaoRespostaService.EhDonoAsync(id, meuId))
    {
        return Results.NotFound();
    }
    try
    {
        var (nomeArquivo, bytes) = await cartaoRespostaService.GerarPdfLoteAsync(id);
        return Results.File(bytes, "application/pdf", nomeArquivo + ".pdf");
    }
    catch (OperacaoInvalidaException)
    {
        return Results.NotFound();
    }
}).RequireAuthorization();

app.MapGet("/cartoes-resposta/cartao/{cartaoId:int}/cartao.pdf", async (int cartaoId, HttpContext http, CartaoRespostaService cartaoRespostaService) =>
{
    var meuId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!await cartaoRespostaService.EhDonoDoCartaoAsync(cartaoId, meuId))
    {
        return Results.NotFound();
    }
    try
    {
        var (nomeArquivo, bytes) = await cartaoRespostaService.GerarPdfUnicoAsync(cartaoId);
        return Results.File(bytes, "application/pdf", nomeArquivo + ".pdf");
    }
    catch (OperacaoInvalidaException)
    {
        return Results.NotFound();
    }
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

// Fora de componente pelo mesmo motivo do logout; diferente do NavMenu, a
// tela de perfil é sempre InteractiveServer, então o antiforgery normal fica ligado.
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

// Cria os papéis "Professor" e "Admin" se ainda não existirem, toda vez que
// a aplicação sobe — evita criar isso manualmente no banco.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Aplica migrations pendentes automaticamente ao subir — precisa vir ANTES
    // de qualquer acesso ao banco (inclusive o RoleManager logo abaixo, que já
    // consulta AspNetRoles). Sem isso, um banco novo (ex.: o de produção/v1)
    // fica sem tabela nenhuma até alguém rodar "dotnet ef database update"
    // manualmente. Idempotente: se já estiver tudo aplicado (caso comum em
    // dev), não faz nada.
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Professor", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Só roda com "dotnet run -- --resetar-questoes"; precisa vir antes dos
    // seeds (idempotentes) senão as questões apagadas ressurgiriam na mesma execução.
    if (args.Contains("--resetar-questoes"))
    {
        await DbReset.ApagarTodasProvasEQuestoesAsync(dbContext);
    }

    // Cada seed só insere o que não existe; criadoPorId dá dono às questões,
    // senão o filtro VisivelPara as esconderia de todo mundo.
    var criadoPorId = await dbContext.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
    await DbSeeder.RepararQuestoesSemDonoAsync(dbContext, criadoPorId);

    // Dependência estrutural da feature ENADE (não é dado de teste), por isso
    // roda sempre, sem depender de --resetar-questoes nem de usuário cadastrado.
    await DbSeeder.SeedDisciplinaFormacaoGeralAsync(dbContext);

    // Questões de exemplo/teste (Engenharia de Software, Matemática, Física,
    // Redes, Programação, ENADE extra...) só em Development — em produção
    // (v1) o banco começa limpo, só com o conteúdo real do professor.
    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.SeedEngenhariaDeSoftwareAsync(dbContext, criadoPorId);
        await DbSeeder.SeedMatematicaAsync(dbContext, criadoPorId);
        await DbSeeder.SeedIntroducaoComputacaoAsync(dbContext, criadoPorId);
        await DbSeederFisica.SeedAsync(dbContext, criadoPorId);
        await DbSeederRedes.SeedAsync(dbContext, criadoPorId);
        await DbSeederProgramacao.SeedAsync(dbContext, criadoPorId);
        await DbSeederMatematicaExtra.SeedAsync(dbContext, criadoPorId);
        await DbSeederEngenhariaSoftwareExtra.SeedAsync(dbContext, criadoPorId);
        await DbSeederEnadeExtra.SeedAsync(dbContext, criadoPorId);
    }

    // Best-effort: só faz algo se o Curso já tiver uma Matriz ENADE/DCN ativa;
    // caso contrário só loga e segue, sem quebrar a inicialização.
    await DbSeederEnadeVinculo.VincularAsync(dbContext, "Engenharia de Computação");

    // Backfill da tag "Com imagem" pra questão anterior a essa sincronização
    // (daqui pra frente QuestaoTagService cuida sozinho); idempotente.
    var questaoTagService = scope.ServiceProvider.GetRequiredService<QuestaoTagService>();
    await questaoTagService.SincronizarTagsDeImagemEmMassaAsync();
}

app.Run();
