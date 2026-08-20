using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Watodoo.Shared.Data;
using Watodoo.Shared.ExternalApis.Igdb;
using Watodoo.Shared.ExternalApis.Wikidata;
using Watodoo.Shared.ExternalApis.Wikipedia;
using Watodoo.Tests.Integration.Games;

namespace Watodoo.Tests.Functional.Games;

public sealed class SeedGamesFromIgdbTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public FakeIgdbClient IgdbClient { get; } = new();

    public FakeWikidataClient WikidataClient { get; } = new();

    public FakeWikipediaClient WikipediaClient { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                    ["ConnectionStrings:Redis"] = "localhost:6379",
                    ["Jwt:Issuer"] = "Watodoo.Tests",
                    ["Jwt:Audience"] = "Watodoo.Tests",
                    ["Jwt:SigningKey"] = "test-only-signing-key-1234567890-abcdefghijkl",
                    ["Igdb:ClientId"] = "test-only-igdb-client-id",
                    ["Igdb:ClientSecret"] = "test-only-igdb-client-secret",
                    ["Ingestion:AdminKey"] = "test-only-admin-key-1234567890",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                }));

            // Remplace les vrais clients externes par des fakes : même si le HangfireServer réel du
            // host ramasse et exécute un job enqueued pendant le test, aucun appel réseau réel n'est
            // fait (ni vers IGDB/Twitch OAuth2, ni vers Wikidata/Wikipédia).
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IIgdbClient>(_ => IgdbClient));
                services.Replace(ServiceDescriptor.Scoped<IWikidataClient>(_ => WikidataClient));
                services.Replace(ServiceDescriptor.Scoped<IWikipediaClient>(_ => WikipediaClient));
            });
        });

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

[CollectionDefinition(nameof(SeedGamesFromIgdbTestCollection))]
public sealed class SeedGamesFromIgdbTestCollection : ICollectionFixture<SeedGamesFromIgdbTestFixture>;
