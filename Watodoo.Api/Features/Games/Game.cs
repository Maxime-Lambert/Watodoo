namespace Watodoo.Features.Games;

public sealed class Game
{
    public required Guid Id { get; init; }

    public required int IgdbId { get; init; }

    public required string TitleFr { get; set; }

    public required string TitleEn { get; set; }

    public string? SynopsisFr { get; set; }

    public string? SynopsisEn { get; set; }

    public string? CoverImageId { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    // Échelle IGDB 0-100 (pas 0-10 comme Film/TvShow) — voir plans/active-plan.md (edge cases).
    public double Rating { get; set; }

    public int RatingCount { get; set; }

    public int[] GenreIds { get; set; } = [];

    // Renseigné par la passe d'enrichissement FR (Features/Games/EnrichFrenchLocalization), jamais
    // par l'ingestion IGDB elle-même.
    public string? WikidataQid { get; set; }

    // Nul tant que la passe d'enrichissement FR n'est pas passée sur ce jeu (match trouvé ou non) :
    // évite de retraiter indéfiniment les jeux sans correspondance Wikidata.
    public DateTimeOffset? FrenchEnrichedAt { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }
}
