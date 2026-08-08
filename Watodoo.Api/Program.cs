using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Watodoo.Configuration;
using Watodoo.Features.Auth;
using Watodoo.Features.Auth.CleanupExpiredRefreshTokens;
using Watodoo.Features.Auth.Login;
using Watodoo.Features.Auth.Logout;
using Watodoo.Features.Auth.Me;
using Watodoo.Features.Auth.Refresh;
using Watodoo.Features.Auth.Register;
using Watodoo.Middleware;
using Watodoo.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<WatodooDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
builder.Services.AddHangfireServer();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<WatodooDbContext>()
    .AddDefaultTokenProviders();

// ValidateOnStart : échoue au démarrage du host (avant toute requête HTTP) si la clé est
// absente/trop courte, plutôt qu'à la première authentification. Fonctionne sous
// WebApplicationFactory car les hosted services de validation démarrent après Build(),
// point auquel les surcharges de config des tests sont déjà appliquées.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration("Jwt")
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey doit être configurée (dotnet user-secrets en local) et faire au moins 32 octets (256 bits) pour HS256.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Lecture différée : sous WebApplicationFactory, les surcharges de config des tests
        // ne sont appliquées qu'après l'exécution synchrone de ce fichier, donc une variable
        // capturée au niveau du builder ignorerait ces surcharges.
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Configuration 'Jwt' manquante (Issuer/Audience/SigningKey).");

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Le backend n'est jamais atteignable autrement que via le conteneur `edge` (Caddy) : en
// prod son port n'est pas publié sur l'hôte, donc n'importe quel appelant direct est de facto
// un proxy de confiance par construction réseau (pas parce qu'on fait confiance à l'en-tête en
// soi) — d'où KnownNetworks/KnownProxies vidés plutôt qu'une liste d'IP à maintenir. Chaîne
// réelle en prod : Client → Cloudflare → Caddy (edge) → backend. Cloudflare pose déjà
// X-Forwarded-For avec l'IP client réelle, puis Caddy y ajoute la sienne en relayant vers le
// backend (append, pas overwrite) : le header reçu ici a donc 2 entrées, la plus à gauche étant
// le vrai client.
// ForwardLimit=2 (pas null) : avec KnownProxies/KnownNetworks vidés, le middleware ne valide
// plus du tout l'origine des entrées — un ForwardLimit illimité laisserait donc un client
// préfixer son propre header avec une IP forgée ("faux-ip, vraie-ip, ip-cloudflare") pour
// changer de partition de rate limiting à volonté, y compris via le chemin normal (pas besoin
// de contourner Cloudflare). En ne consommant que les 2 entrées les plus à droite (celles
// posées par Cloudflare et Caddy, jamais par le client), toute entrée forgée à gauche est
// ignorée.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 2;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60)),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<RegisterCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<RefreshCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();
builder.Services.AddScoped<GetMeQueryHandler>();
builder.Services.AddScoped<CleanupExpiredRefreshTokensJob>();

var app = builder.Build();

// Doit s'exécuter avant tout code lisant RemoteIpAddress (rate limiter, logs...).
app.UseForwardedHeaders();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapHangfireDashboard();
}

// Planifié à 3h du matin UTC, décalé du backup PostgreSQL (2h, voir architecture.md) — dans
// tous les environnements (pas seulement prod) pour que le comportement soit identique en local.
RecurringJob.AddOrUpdate<CleanupExpiredRefreshTokensJob>(
    "cleanup-expired-refresh-tokens",
    job => job.RunAsync(),
    Cron.Daily(3));

if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<WatodooDbContext>().Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();

app.Run();

public partial class Program;
