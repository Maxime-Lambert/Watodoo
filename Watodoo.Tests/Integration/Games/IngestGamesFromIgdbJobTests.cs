using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;
using Watodoo.Features.Games.IngestFromIgdb;
using Watodoo.Shared.Data;
using Watodoo.Shared.ExternalApis.Igdb;

namespace Watodoo.Tests.Integration.Games;

[Collection(nameof(GamesDbCollection))]
public sealed class IngestGamesFromIgdbJobTests(GamesDbFixture fixture)
{
    // PageSize interne du job (voir IngestGamesFromIgdbJob) : les offsets utilisés dans ces tests
    // (0, 500, 1000...) doivent correspondre à ce pas fixe.
    private const int PageSize = 500;

    private static IgdbGameDto CreateGame(int id, string name = "Jeu", double totalRating = 50.0) => new()
    {
        Id = id,
        Name = name,
        Summary = "Synopsis.",
        FirstReleaseDate = 1704067200, // 2024-01-01T00:00:00Z
        TotalRating = totalRating,
        TotalRatingCount = 10,
        Genres = [12],
    };

    private static IngestGamesFromIgdbJob CreateJob(WatodooDbContext db, FakeIgdbClient client, int nightlyLookbackDays = 3) =>
        new(
            db,
            client,
            Options.Create(new IgdbOptions { ClientId = "test", ClientSecret = "test", NightlyLookbackDays = nightlyLookbackDays }),
            NullLogger<IngestGamesFromIgdbJob>.Instance);

    [Fact]
    public async Task SeedAsync_inserts_new_games_from_a_single_page()
    {
        await using var context = fixture.CreateContext();
        var client = new FakeIgdbClient();
        client.SetPage(0, [CreateGame(101, "Jeu Un"), CreateGame(102, "Jeu Deux")]);

        await CreateJob(context, client).SeedAsync(maxItems: 5);

        await using var verify = fixture.CreateContext();
        Assert.Equal(2, await verify.Games.CountAsync(g => g.IgdbId == 101 || g.IgdbId == 102));
    }

    [Fact]
    public async Task SeedAsync_updates_existing_game_instead_of_duplicating()
    {
        await using var seedContext = fixture.CreateContext();
        var seedClient = new FakeIgdbClient();
        seedClient.SetPage(0, [CreateGame(201, "Titre", totalRating: 50.0)]);
        await CreateJob(seedContext, seedClient).SeedAsync(maxItems: 5);

        await using var firstVerify = fixture.CreateContext();
        var createdAt = (await firstVerify.Games.SingleAsync(g => g.IgdbId == 201)).CreatedAt;

        await using var updateContext = fixture.CreateContext();
        var updateClient = new FakeIgdbClient();
        updateClient.SetPage(0, [CreateGame(201, "Titre", totalRating: 91.0)]);
        await CreateJob(updateContext, updateClient).SeedAsync(maxItems: 5);

        await using var verify = fixture.CreateContext();
        var games = await verify.Games.Where(g => g.IgdbId == 201).ToListAsync();
        Assert.Single(games);
        Assert.Equal(91.0, games[0].Rating);
        Assert.Equal(createdAt, games[0].CreatedAt);
    }

    [Fact]
    public async Task SeedAsync_does_not_overwrite_french_enrichment_already_done()
    {
        await using var seedContext = fixture.CreateContext();
        var seedClient = new FakeIgdbClient();
        seedClient.SetPage(0, [CreateGame(210, "Nom EN")]);
        await CreateJob(seedContext, seedClient).SeedAsync(maxItems: 5);

        await using var enrichContext = fixture.CreateContext();
        var game = await enrichContext.Games.SingleAsync(g => g.IgdbId == 210);
        game.TitleFr = "Nom FR enrichi";
        game.SynopsisFr = "Résumé FR enrichi.";
        game.WikidataQid = "Q999";
        game.FrenchEnrichedAt = DateTimeOffset.UtcNow;
        await enrichContext.SaveChangesAsync();

        // Relu depuis la base (pas la valeur en mémoire ci-dessus) : Postgres tronque
        // DateTimeOffset à la microseconde (7 décimales .NET vs 6 côté "timestamp with time zone"),
        // comparer contre la valeur déjà passée par la base évite un faux négatif de précision.
        await using var enrichedVerify = fixture.CreateContext();
        var enrichedAt = (await enrichedVerify.Games.SingleAsync(g => g.IgdbId == 210)).FrenchEnrichedAt;

        await using var reseedContext = fixture.CreateContext();
        var reseedClient = new FakeIgdbClient();
        reseedClient.SetPage(0, [CreateGame(210, "Nouveau nom EN")]);
        await CreateJob(reseedContext, reseedClient).SeedAsync(maxItems: 5);

        await using var verify = fixture.CreateContext();
        var updated = await verify.Games.SingleAsync(g => g.IgdbId == 210);
        Assert.Equal("Nouveau nom EN", updated.TitleEn);
        Assert.Equal("Nom FR enrichi", updated.TitleFr);
        Assert.Equal("Résumé FR enrichi.", updated.SynopsisFr);
        Assert.Equal("Q999", updated.WikidataQid);
        Assert.Equal(enrichedAt, updated.FrenchEnrichedAt);
    }

    [Fact]
    public async Task SeedAsync_stops_when_a_page_is_empty()
    {
        await using var context = fixture.CreateContext();
        var client = new FakeIgdbClient();
        client.SetPage(0, [CreateGame(301, "Solo")]);
        // Offset PageSize jamais peuplé côté fake => résultats vides par défaut. Le job doit
        // interroger cet offset (c'est ce qui lui révèle qu'il est vide) puis s'arrêter.

        await CreateJob(context, client).SeedAsync(maxItems: 10 * PageSize);

        Assert.Equal(2, client.ReceivedQueries.Count);
        Assert.Equal([0, PageSize], client.ReceivedQueries.Select(q => q.Offset));
    }

    [Fact]
    public async Task SeedAsync_never_requests_more_items_than_maxItems()
    {
        await using var context = fixture.CreateContext();
        var client = new FakeIgdbClient();
        for (var i = 0; i < 3; i++)
        {
            client.SetPage(i * PageSize, [CreateGame(400 + i, $"Jeu {i}")]);
        }

        await CreateJob(context, client).SeedAsync(maxItems: 2 * PageSize);

        Assert.Equal(2, client.ReceivedQueries.Count);
    }

    [Fact]
    public async Task RefreshNightlyAsync_filters_by_the_configured_lookback_window()
    {
        await using var context = fixture.CreateContext();
        var client = new FakeIgdbClient();
        // Pas d'offset 0 configuré => résultats vides, cas nominal (aucune sortie récente).

        await CreateJob(context, client, nightlyLookbackDays: 5).RefreshNightlyAsync();

        var query = Assert.Single(client.ReceivedQueries);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), query.ReleaseDateFrom);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), query.ReleaseDateTo);
    }
}
