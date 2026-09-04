using ErpFinanceiro.Application.Categorias;
using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Categorias;
using ErpFinanceiro.Infrastructure.Data;
using ErpFinanceiro.Infrastructure.Seguranca;
using ErpFinanceiro.Infrastructure.Usuarios;
using ErpFinanceiro.Web.Components;
using ErpFinanceiro.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

// Cookie de autenticação: HTTPS obrigatório, SameSite estrito e expiração
// alinhada a uma jornada de trabalho (em vez dos 14 dias default) — reduz
// a janela de uma sessão esquecida em máquina compartilhada.
builder.Services.ConfigureApplicationCookie(options =>
{
    // Always em produção; em dev local (sem HTTPS configurado no container)
    // isso derrubaria o cookie silenciosamente — SameAsRequest é o próprio
    // default do Identity.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IGerenciadorUsuarios, GerenciadorUsuarios>();
builder.Services.AddScoped(typeof(IGerenciadorCadastroSimples<>), typeof(GerenciadorCadastroSimples<>));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoints exigidos pelos componentes de Identity em Components/Account.
app.MapAdditionalIdentityEndpoints();

using (var scope = app.Services.CreateScope())
{
    await SeedInicial.AplicarAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

app.Run();
