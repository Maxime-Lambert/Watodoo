using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Watodoo.Shared.Data;

namespace Watodoo.Tests.Functional;

public sealed class FunctionalTestFixture : IAsyncLifetime
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
                    // Config de test explicite : ne dépend jamais de dotnet user-secrets (absent en CI).
                    ["Jwt:Issuer"] = "Watodoo.Tests",
                    ["Jwt:Audience"] = "Watodoo.Tests",
                    ["Jwt:SigningKey"] = "test-only-signing-key-1234567890-abcdefghijkl",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
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

[CollectionDefinition(nameof(FunctionalTestCollection))]
public sealed class FunctionalTestCollection : ICollectionFixture<FunctionalTestFixture>;
