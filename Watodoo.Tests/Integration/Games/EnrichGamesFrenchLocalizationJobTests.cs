using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Watodoo.Features.Games;
using Watodoo.Features.Games.EnrichFrenchLocalization;
using Watodoo.Shared.Data;
using Watodoo.Shared.ExternalApis.Wikidata;

namespace Watodoo.Tests.Integration.Games;

[Collection(nameof(GamesDbCollection))]
public sealed class EnrichGamesFrenchLocalizationJobTests(GamesDbFixture fixture)
{
    private static Game CreateGame(int igdbId, DateTimeOffset? createdAt = null, DateTimeOffset? frenchEnrichedAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new Game
        {
            Id = Guid.NewGuid(),
            IgdbId = igdbId,
            TitleFr = "Nom EN",
            TitleEn = "Nom EN",
            SynopsisFr = "Synopsis EN.",
            SynopsisEn = "Synopsis EN.",
            CreatedAt = now,
            UpdatedAt = now,
            FrenchEnrichedAt = frenchEnrichedAt,
        };
    }

    private static EnrichGamesFrenchLocalizationJob CreateJob(WatodooDbContext db, FakeWikidataClient wikidata, FakeWikipediaClient wikipedia) =>
        new(db, wikidata, wikipedia, NullLogger<EnrichGamesFrenchLocalizationJob>.Instance);

    // Le job traite TOUS les jeux non enrichis de la base (pas de filtre par IgdbId, contrairement
    // aux tests de IngestGamesFromIgdbJobTests) : GamesDbFixture est un Postgres partagé, sans reset
    // entre tests. Sans ce nettoyage, des jeux non enrichis laissés par un autre test de cette classe
    // fausseraient les assertions sur le contenu exact du lot traité.
    private static async Task ResetGamesAsync(WatodooDbContext context) => await context.Games.ExecuteDeleteAsync();

    [Fact]
    public async Task Game_with_full_match_updates_title_synopsis_and_wikidata_qid()
    {
        await using var context = fixture.CreateContext();
        await ResetGamesAsync(context);
        context.Games.Add(CreateGame(501));
        await context.SaveChangesAsync();

        var wikidata = new FakeWikidataClient();
        wikidata.SetMatch("Nom EN", new WikidataMatchDto("Q1", "Nom Français", "Nom Français (jeu vidéo)"));
        var wikipedia = new FakeWikipediaClient();
        wikipedia.SetExtract("Nom Français (jeu vidéo)", "Résumé trouvé sur Wikipédia.");

        await CreateJob(context, wikidata, wikipedia).RunAsync(batchSize: 100);

        await using var verify = fixture.CreateContext();
        var game = await verify.Games.SingleAsync(g => g.IgdbId == 501);
        Assert.Equal("Nom Français", game.TitleFr);
        Assert.Equal("Résumé trouvé sur Wikipédia.", game.SynopsisFr);
        Assert.Equal("Q1", game.WikidataQid);
        Assert.NotNull(game.FrenchEnrichedAt);
        // Le contenu EN, posé indépendamment par l'ingestion IGDB, n'est jamais touché par cette passe.
        Assert.Equal("Nom EN", game.TitleEn);
    }

    [Fact]
    public async Task Game_without_wikidata_match_keeps_igdb_fallback_but_is_marked_enriched()
    {
        await using var context = fixture.CreateContext();
        await ResetGamesAsync(context);
        context.Games.Add(CreateGame(502));
        await context.SaveChangesAsync();

        await CreateJob(context, new FakeWikidataClient(), new FakeWikipediaClient()).RunAsync(batchSize: 100);

        await using var verify = fixture.CreateContext();
        var game = await verify.Games.SingleAsync(g => g.IgdbId == 502);
        Assert.Equal("Nom EN", game.TitleFr);
        Assert.Equal("Synopsis EN.", game.SynopsisFr);
        Assert.Null(game.WikidataQid);
        Assert.NotNull(game.FrenchEnrichedAt);
    }

    [Fact]
    public async Task Match_without_frwiki_sitelink_updates_title_but_not_synopsis()
    {
        await using var context = fixture.CreateContext();
        await ResetGamesAsync(context);
        context.Games.Add(CreateGame(503));
        await context.SaveChangesAsync();

        var wikidata = new FakeWikidataClient();
        wikidata.SetMatch("Nom EN", new WikidataMatchDto("Q2", "Nom Français Seul", FrWikipediaTitle: null));

        await CreateJob(context, wikidata, new FakeWikipediaClient()).RunAsync(batchSize: 100);

        await using var verify = fixture.CreateContext();
        var game = await verify.Games.SingleAsync(g => g.IgdbId == 503);
        Assert.Equal("Nom Français Seul", game.TitleFr);
        Assert.Equal("Synopsis EN.", game.SynopsisFr);
    }

    [Fact]
    public async Task Already_enriched_game_is_excluded_from_the_batch()
    {
        await using var context = fixture.CreateContext();
        await ResetGamesAsync(context);
        context.Games.Add(CreateGame(504, frenchEnrichedAt: DateTimeOffset.UtcNow.AddDays(-1)));
        await context.SaveChangesAsync();

        var wikidata = new FakeWikidataClient();

        await CreateJob(context, wikidata, new FakeWikipediaClient()).RunAsync(batchSize: 100);

        Assert.Empty(wikidata.ReceivedTitles);
    }

    [Fact]
    public async Task Respects_batch_size()
    {
        await using var context = fixture.CreateContext();
        await ResetGamesAsync(context);
        for (var i = 0; i < 5; i++)
        {
            context.Games.Add(CreateGame(600 + i, createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        await context.SaveChangesAsync();

        var wikidata = new FakeWikidataClient();

        await CreateJob(context, wikidata, new FakeWikipediaClient()).RunAsync(batchSize: 2);

        Assert.Equal(2, wikidata.ReceivedTitles.Count);
    }
}
