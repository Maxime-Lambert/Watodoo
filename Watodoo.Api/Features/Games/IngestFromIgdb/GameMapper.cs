using Watodoo.Shared.ExternalApis.Igdb;

namespace Watodoo.Features.Games.IngestFromIgdb;

public static class GameMapper
{
    public static Game ToNewEntity(IgdbGameDto dto)
    {
        var now = DateTimeOffset.UtcNow;

        return new Game
        {
            Id = Guid.NewGuid(),
            IgdbId = dto.Id,
            // IGDB ne fournit pas de contenu par langue : TitleFr/SynopsisFr sont initialisés sur la
            // même valeur qu'TitleEn/SynopsisEn ici, et seront écrasés par la passe d'enrichissement FR
            // (Features/Games/EnrichFrenchLocalization) si une correspondance Wikidata est trouvée.
            TitleFr = dto.Name,
            TitleEn = dto.Name,
            SynopsisFr = NullIfEmpty(dto.Summary),
            SynopsisEn = NullIfEmpty(dto.Summary),
            CoverImageId = dto.Cover?.ImageId,
            ReleaseDate = ToReleaseDate(dto.FirstReleaseDate),
            Rating = dto.TotalRating ?? 0,
            RatingCount = dto.TotalRatingCount ?? 0,
            GenreIds = [.. dto.Genres],
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // Ne touche jamais Id/IgdbId/CreatedAt/WikidataQid/FrenchEnrichedAt/TitleFr/SynopsisFr : un
    // re-seed IGDB ne doit jamais écraser un enrichissement FR déjà réalisé (Phase 3).
    public static void ApplyTo(Game existing, IgdbGameDto dto)
    {
        existing.TitleEn = dto.Name;
        existing.SynopsisEn = NullIfEmpty(dto.Summary);
        existing.CoverImageId = dto.Cover?.ImageId;
        existing.ReleaseDate = ToReleaseDate(dto.FirstReleaseDate);
        existing.Rating = dto.TotalRating ?? 0;
        existing.RatingCount = dto.TotalRatingCount ?? 0;
        existing.GenreIds = [.. dto.Genres];
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateOnly? ToReleaseDate(long? unixSeconds) =>
        unixSeconds is { } seconds
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime)
            : null;
}
