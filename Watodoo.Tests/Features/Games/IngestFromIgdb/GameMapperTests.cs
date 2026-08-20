using Watodoo.Features.Games.IngestFromIgdb;
using Watodoo.Shared.ExternalApis.Igdb;

namespace Watodoo.Tests.Features.Games.IngestFromIgdb;

public sealed class GameMapperTests
{
    private static IgdbGameDto CreateGameDto(
        int id = 42,
        string name = "Un Jeu",
        string? summary = "Un synopsis.",
        long? firstReleaseDate = 1714521600, // 2024-05-01T00:00:00Z
        double? totalRating = 75.5,
        int? totalRatingCount = 1000,
        List<int>? genres = null,
        string? coverImageId = "abc123") =>
        new()
        {
            Id = id,
            Name = name,
            Summary = summary,
            FirstReleaseDate = firstReleaseDate,
            TotalRating = totalRating,
            TotalRatingCount = totalRatingCount,
            Genres = genres ?? [12, 31],
            Cover = coverImageId is null ? null : new IgdbCoverDto { ImageId = coverImageId },
        };

    [Fact]
    public void ToNewEntity_maps_name_and_summary_to_both_languages()
    {
        var dto = CreateGameDto(name: "The Witcher 3", summary: "Un sorceleur.");

        var game = GameMapper.ToNewEntity(dto);

        Assert.Equal(dto.Id, game.IgdbId);
        Assert.Equal("The Witcher 3", game.TitleFr);
        Assert.Equal("The Witcher 3", game.TitleEn);
        Assert.Equal("Un sorceleur.", game.SynopsisFr);
        Assert.Equal("Un sorceleur.", game.SynopsisEn);
        Assert.Equal(new DateOnly(2024, 5, 1), game.ReleaseDate);
        Assert.Equal(75.5, game.Rating);
        Assert.Equal(1000, game.RatingCount);
        Assert.Equal([12, 31], game.GenreIds);
        Assert.Equal("abc123", game.CoverImageId);
        Assert.Null(game.WikidataQid);
        Assert.Null(game.FrenchEnrichedAt);
    }

    [Fact]
    public void ToNewEntity_maps_missing_summary_to_null_synopsis()
    {
        var dto = CreateGameDto(summary: null);

        var game = GameMapper.ToNewEntity(dto);

        Assert.Null(game.SynopsisFr);
        Assert.Null(game.SynopsisEn);
    }

    [Fact]
    public void ToNewEntity_maps_empty_summary_to_null_synopsis()
    {
        var dto = CreateGameDto(summary: "   ");

        var game = GameMapper.ToNewEntity(dto);

        Assert.Null(game.SynopsisFr);
        Assert.Null(game.SynopsisEn);
    }

    [Fact]
    public void ToNewEntity_maps_missing_release_date_to_null()
    {
        var dto = CreateGameDto(firstReleaseDate: null);

        var game = GameMapper.ToNewEntity(dto);

        Assert.Null(game.ReleaseDate);
    }

    [Fact]
    public void ToNewEntity_maps_missing_rating_to_zero()
    {
        var dto = CreateGameDto(totalRating: null, totalRatingCount: null);

        var game = GameMapper.ToNewEntity(dto);

        Assert.Equal(0, game.Rating);
        Assert.Equal(0, game.RatingCount);
    }

    [Fact]
    public void ToNewEntity_maps_missing_cover_to_null()
    {
        var dto = CreateGameDto(coverImageId: null);

        var game = GameMapper.ToNewEntity(dto);

        Assert.Null(game.CoverImageId);
    }

    [Fact]
    public void ApplyTo_preserves_identity_creation_date_and_french_content_but_updates_english_and_metadata()
    {
        var original = GameMapper.ToNewEntity(CreateGameDto(totalRating: 60.0));
        original.TitleFr = "Titre français manuel";
        original.SynopsisFr = "Résumé français manuel.";
        original.WikidataQid = "Q12345";
        original.FrenchEnrichedAt = DateTimeOffset.UtcNow;

        var originalId = original.Id;
        var originalIgdbId = original.IgdbId;
        var originalCreatedAt = original.CreatedAt;
        var originalTitleFr = original.TitleFr;
        var originalSynopsisFr = original.SynopsisFr;
        var originalWikidataQid = original.WikidataQid;
        var originalFrenchEnrichedAt = original.FrenchEnrichedAt;

        var updatedDto = CreateGameDto(name: "Nouveau nom EN", totalRating: 88.2, totalRatingCount: 5000);
        GameMapper.ApplyTo(original, updatedDto);

        Assert.Equal(originalId, original.Id);
        Assert.Equal(originalIgdbId, original.IgdbId);
        Assert.Equal(originalCreatedAt, original.CreatedAt);
        Assert.Equal(originalTitleFr, original.TitleFr);
        Assert.Equal(originalSynopsisFr, original.SynopsisFr);
        Assert.Equal(originalWikidataQid, original.WikidataQid);
        Assert.Equal(originalFrenchEnrichedAt, original.FrenchEnrichedAt);
        Assert.Equal("Nouveau nom EN", original.TitleEn);
        Assert.Equal(88.2, original.Rating);
        Assert.Equal(5000, original.RatingCount);
        Assert.True(original.UpdatedAt >= originalCreatedAt);
    }
}
