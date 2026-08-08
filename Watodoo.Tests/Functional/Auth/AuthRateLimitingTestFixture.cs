using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Watodoo.Shared.Data;

namespace Watodoo.Tests.Functional.Auth;

public sealed class AuthRateLimitingTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                    ["ConnectionStrings:Redis"] = "localhost:6379",
                    ["Jwt:Issuer"] = "Watodoo.Tests",
                    ["Jwt:Audience"] = "Watodoo.Tests",
                    ["Jwt:SigningKey"] = "test-only-signing-key-1234567890-abcdefghijkl",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                    // Limite volontairement basse (contrairement à FunctionalTestFixture qui la relève) :
                    // ces tests vérifient le comportement exact au seuil du rate limiter.
                    ["RateLimiting:Auth:PermitLimit"] = "3",
                    ["RateLimiting:Auth:WindowSeconds"] = "60",
                })));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WatodooDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(AuthRateLimitingTestCollection))]
public sealed class AuthRateLimitingTestCollection : ICollectionFixture<AuthRateLimitingTestFixture>;
