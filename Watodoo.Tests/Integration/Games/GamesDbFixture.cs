using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Watodoo.Shared.Data;

namespace Watodoo.Tests.Integration.Games;

public sealed class GamesDbFixture : IAsyncLifetime
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

[CollectionDefinition(nameof(GamesDbCollection))]
public sealed class GamesDbCollection : ICollectionFixture<GamesDbFixture>;
