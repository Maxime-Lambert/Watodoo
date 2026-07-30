using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Watodoo.Shared.Data;

namespace Watodoo.Tests.Integration.Auth;

public sealed class AuthDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public WatodooDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WatodooDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new WatodooDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

[CollectionDefinition(nameof(AuthDbCollection))]
public sealed class AuthDbCollection : ICollectionFixture<AuthDbFixture>;
