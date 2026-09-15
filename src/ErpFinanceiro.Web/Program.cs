using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Application.Categorias;
using ErpFinanceiro.Application.ChatIa;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Application.Relatorios;
using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure;
using ErpFinanceiro.Infrastructure.Anexos;
using ErpFinanceiro.Infrastructure.Auditoria;
using ErpFinanceiro.Infrastructure.Boletos;
using ErpFinanceiro.Infrastructure.Cartoes;
using ErpFinanceiro.Infrastructure.Categorias;
using ErpFinanceiro.Infrastructure.ChatIa;
using ErpFinanceiro.Infrastructure.ConfiguracoesIa;
using ErpFinanceiro.Infrastructure.ContasAPagar;
using ErpFinanceiro.Infrastructure.Dashboard;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.DocumentosIa;
using ErpFinanceiro.Infrastructure.Fornecedores;
using ErpFinanceiro.Infrastructure.NotasFiscais;
using ErpFinanceiro.Infrastructure.Relatorios;
using ErpFinanceiro.Infrastructure.Seguranca;
using ErpFinanceiro.Infrastructure.Storage;
using ErpFinanceiro.Infrastructure.Usuarios;
using ErpFinanceiro.Web.Components;
using ErpFinanceiro.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

// Sistema brasileiro: toda formatação de data/moeda (ToString("C"), etc.)
// deve usar pt-BR (R$, vírgula decimal), não a cultura invariante do
// container (que mostra "¤" no lugar de "R$"). Setado antes do
// CreateBuilder para valer em toda a aplicação, inclusive em threads de
// background (seed, etc.).
var culturaPtBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPtBr;
CultureInfo.DefaultThreadCurrentUICulture = culturaPtBr;

var builder = WebApplication.CreateBuilder(args);
// Opt-in para instalação local sem TLS; mantém o ambiente Production.
var permitirHttpLocal = builder.Configuration.GetValue<bool>("Seguranca:PermitirHttpLocal");

// Persistência das chaves de cookies entre recriações do container.
var diretorioChaves = builder.Configuration["DataProtection:DiretorioChaves"];
if (!string.IsNullOrWhiteSpace(diretorioChaves))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("ErpFinanceiro")
        .PersistKeysToFileSystem(new DirectoryInfo(diretorioChaves));
}

// Confia somente nos endereços de proxy configurados pelo operador.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var endereco in (builder.Configuration["Proxy:EnderecosConfiaveis"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        options.KnownProxies.Add(IPAddress.Parse(endereco));
    }
});
builder.Services.AddHealthChecks()
    .AddCheck<ErpFinanceiro.Web.Servicos.BancoHealthCheck>("banco");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddSingleton(new CriptografiaAes256(builder.Configuration["Criptografia:ChaveAes256Base64"] ?? ""));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<Usuario>(options =>
    {
        // Não há fluxo de confirmação de e-mail (usuários são criados por um
        // Administrador, não por auto-cadastro) — ver seção 15 do escopo.
        options.SignIn.RequireConfirmedAccount = false;

        // Política de senha explícita (mesmo repetindo defaults do Identity)
        // para ficar auditável no código, não implícita no framework —
        // recomendação do security-auditor no Passo 4.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Lockout explícito (15 min é mais dissuasório que o default de 5 min
        // contra força bruta, sem travar demais um usuário legítimo).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Cookie de autenticação: HTTPS por padrão, SameSite estrito e expiração
// alinhada a uma jornada de trabalho (em vez dos 14 dias default) — reduz
// a janela de uma sessão esquecida em máquina compartilhada.
builder.Services.ConfigureApplicationCookie(options =>
{
    // HTTP local exige cookie compatível com o protocolo da requisição.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || permitirHttpLocal
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IGerenciadorUsuarios, GerenciadorUsuarios>();
builder.Services.AddScoped(typeof(IGerenciadorCadastroSimples<>), typeof(GerenciadorCadastroSimples<>));
builder.Services.AddScoped<IGerenciadorFornecedores, GerenciadorFornecedores>();
builder.Services.AddScoped<IGerenciadorContasBancariasEmpresa, GerenciadorContasBancariasEmpresa>();
builder.Services.AddScoped<IGerenciadorCartoes, GerenciadorCartoes>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRegistradorAuditoria, RegistradorAuditoria>();
builder.Services.AddScoped<ITimelineConsulta, TimelineConsulta>();
builder.Services.AddScoped<IConsultaAuditoria, ConsultaAuditoria>();
builder.Services.AddScoped<IGerenciadorContasPagar, GerenciadorContasPagar>();
builder.Services.AddScoped<IFluxoAprovacao, FluxoAprovacao>();
builder.Services.AddScoped<IGerenciadorPagamentos, GerenciadorPagamentos>();
builder.Services.AddScoped<IPainelFinanceiro, PainelFinanceiro>();
builder.Services.AddSingleton<IRelogio, RelogioSistema>();

var opcoesAnexos = new OpcoesArmazenamentoAnexos
{
    RaizFisica = builder.Configuration["Anexos:RaizFisica"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "storage"),
};
if (long.TryParse(builder.Configuration["Anexos:TamanhoMaximoBytes"], out var maxBytes))
{
    opcoesAnexos.TamanhoMaximoBytes = maxBytes;
}

builder.Services.AddSingleton(opcoesAnexos);
builder.Services.AddScoped<IArmazenamentoAnexos, ArmazenamentoAnexosDisco>();
builder.Services.AddScoped<IGerenciadorAnexos, GerenciadorAnexos>();
builder.Services.AddScoped<IGerenciadorNotasFiscais, GerenciadorNotasFiscais>();
builder.Services.AddScoped<IGerenciadorBoletos, GerenciadorBoletos>();
builder.Services.AddScoped<IExportadorContasPagar, ExportadorContasPagar>();

// Configuração de IA (provedor/modelo/chave), editável pelo Administrador
// em /configuracoes-ia — Scoped de propósito, nunca cacheada, pra trocar
// valer na próxima chamada sem reiniciar o app.
builder.Services.AddHttpClient<IGerenciadorConfiguracaoIa, GerenciadorConfiguracaoIa>();

// Módulo de IA para documentos (docs/modulo-ia-documentos.md). Fila
// in-process + worker; leitor trocável por configuração (Stub | Http).
builder.Services.AddSingleton<IFilaProcessamentoDocumentos, FilaProcessamentoDocumentos>();
builder.Services.AddScoped<IGerenciadorDocumentosImportados, GerenciadorDocumentosImportados>();
builder.Services.AddHostedService<ErpFinanceiro.Web.Servicos.ProcessadorDocumentosHostedService>();

if (string.Equals(builder.Configuration["Ia:Leitor:Modo"], "Http", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ILeitorDocumentos, LeitorDocumentosHttp>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Ia:Leitor:BaseUrl"] ?? "http://localhost:8000");
        client.Timeout = TimeSpan.FromSeconds(
            int.TryParse(builder.Configuration["Ia:Leitor:TimeoutSegundos"], out var segundos) ? segundos : 120);

        // Mesmo segredo do INTERNAL_TOKEN do serviço Python (.env de lá) —
        // sem isso configurado dos dois lados, /extrair fica aberto pra
        // qualquer host que alcance a porta (achado da auditoria de
        // segurança). Vazio nos dois = checagem desativada (padrão em dev).
        var tokenInterno = builder.Configuration["Ia:Leitor:TokenInterno"];
        if (!string.IsNullOrWhiteSpace(tokenInterno))
        {
            client.DefaultRequestHeaders.Add("X-Internal-Token", tokenInterno);
        }
    });
}
else
{
    builder.Services.AddSingleton<ILeitorDocumentos, LeitorDocumentosStub>();
}

// Agente de chat — catálogo fixo de ferramentas somente-leitura (ver
// CatalogoFerramentasChatIa), provedor escolhido a cada chamada via
// ConfiguracaoIa (finalidade Chat). Hoje só Gemini tem adapter.
builder.Services.AddScoped<ExecutorFerramentasChatIa>();
builder.Services.AddHttpClient<AgenteChatIaGemini>();
builder.Services.AddScoped<IAgenteChatIa, AgenteChatIaFactory>();

// Licença Community do QuestPDF (uso gratuito para empresas pequenas/OSS —
// exigido pela biblioteca desde a v2023, sem isso ela lança exceção em
// tempo de execução ao gerar o primeiro PDF).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

app.UseForwardedHeaders();
// O probe interno precisa responder por HTTP, sem redirecionar para HTTPS.
app.UseHealthChecks("/health");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    if (!permitirHttpLocal)
    {
        app.UseHsts();
    }
}

if (!permitirHttpLocal)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoints exigidos pelos componentes de Identity em Components/Account.
app.MapAdditionalIdentityEndpoints();

// Download de anexo — precisa ser um endpoint HTTP (não dá para servir um
// arquivo direto de um componente Blazor Server). Exige autenticação;
// o storage já bloqueia path traversal ao resolver o caminho.
app.MapGet("/anexos/{id:guid}", async (Guid id, IGerenciadorAnexos gerenciador) =>
{
    var anexo = await gerenciador.BaixarAsync(id);
    return anexo is null
        ? Results.NotFound()
        : Results.File(anexo.Conteudo, anexo.TipoConteudo, anexo.NomeArquivo);
}).RequireAuthorization();

// Visualização do arquivo original de um documento importado (Central de
// Documentos). Mesmo motivo dos anexos: download binário não sai de um
// componente Blazor Server.
app.MapGet("/documentos-importados/{id:guid}/arquivo",
    async (Guid id, System.Security.Claims.ClaimsPrincipal principal, IGerenciadorDocumentosImportados gerenciador,
        IArmazenamentoAnexos storage, Microsoft.AspNetCore.Identity.UserManager<Usuario> userManager) =>
    {
        var documento = await gerenciador.ObterAsync(id);
        if (documento is null)
        {
            return Results.NotFound();
        }

        // Antes da revisão, o documento importado (comprovante/contrato/nota
        // ainda não virou anexo "oficial") só é visível pra quem enviou ou
        // pra Administrador — mesma restrição aplicada em CentralDocumentos.razor
        // (achado da auditoria de segurança: endpoint expunha qualquer
        // documento de qualquer usuário pra qualquer usuário autenticado).
        var usuario = await userManager.GetUserAsync(principal);
        var ehAdministrador = usuario is not null && await userManager.IsInRoleAsync(usuario, nameof(PerfilUsuario.Administrador));
        if (usuario is null || (!ehAdministrador && documento.EnviadoPorId != usuario.Id))
        {
            return Results.Forbid();
        }

        var conteudo = await storage.AbrirAsync(documento.CaminhoArmazenamento);
        return Results.File(conteudo, documento.TipoConteudo, documento.NomeArquivo);
    }).RequireAuthorization();

// Exportação do relatório de contas a pagar (seção 10 do escopo) — mesmo
// motivo dos anexos: download de arquivo binário não sai de um componente
// Blazor Server, precisa ser um endpoint HTTP de verdade. O filtro chega
// como querystring porque é um link <a href>, não um POST de formulário.
async Task<ContaPagar[]> ContasFiltradasAsync(IGerenciadorContasPagar gerenciador, Guid? fornecedorId,
    StatusFinanceiro? statusFinanceiro, StatusAprovacao? statusAprovacao, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal)
{
    var filtro = new FiltroContasPagar(fornecedorId, statusFinanceiro, statusAprovacao, vencimentoInicial, vencimentoFinal);
    var contas = await gerenciador.ListarAsync(filtro);
    return contas.ToArray();
}

app.MapGet("/relatorios/contas-a-pagar.xlsx", async (IGerenciadorContasPagar gerenciador, IExportadorContasPagar exportador,
        Guid? fornecedorId, StatusFinanceiro? statusFinanceiro, StatusAprovacao? statusAprovacao, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal) =>
    {
        var contas = await ContasFiltradasAsync(gerenciador, fornecedorId, statusFinanceiro, statusAprovacao, vencimentoInicial, vencimentoFinal);
        var arquivo = exportador.ExportarExcel(contas);
        return Results.File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "contas-a-pagar.xlsx");
    }).RequireAuthorization();

app.MapGet("/relatorios/contas-a-pagar.csv", async (IGerenciadorContasPagar gerenciador, IExportadorContasPagar exportador,
        Guid? fornecedorId, StatusFinanceiro? statusFinanceiro, StatusAprovacao? statusAprovacao, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal) =>
    {
        var contas = await ContasFiltradasAsync(gerenciador, fornecedorId, statusFinanceiro, statusAprovacao, vencimentoInicial, vencimentoFinal);
        var arquivo = exportador.ExportarCsv(contas);
        return Results.File(arquivo, "text/csv", "contas-a-pagar.csv");
    }).RequireAuthorization();

app.MapGet("/relatorios/contas-a-pagar.pdf", async (IGerenciadorContasPagar gerenciador, IExportadorContasPagar exportador,
        Guid? fornecedorId, StatusFinanceiro? statusFinanceiro, StatusAprovacao? statusAprovacao, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal) =>
    {
        var contas = await ContasFiltradasAsync(gerenciador, fornecedorId, statusFinanceiro, statusAprovacao, vencimentoInicial, vencimentoFinal);
        var arquivo = exportador.ExportarPdf(contas);
        return Results.File(arquivo, "application/pdf", "contas-a-pagar.pdf");
    }).RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    // Habilitado na stack de uma única instância, antes do seed e do worker.
    if (app.Configuration.GetValue<bool>("Banco:AplicarMigracoes"))
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
    await SeedInicial.AplicarAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

app.Run();
