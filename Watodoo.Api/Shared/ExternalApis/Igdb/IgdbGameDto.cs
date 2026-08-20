using System.Text.Json.Serialization;

namespace Watodoo.Shared.ExternalApis.Igdb;

public sealed class IgdbGameDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    // Unix timestamp (secondes) côté IGDB, absent pour les jeux sans date de sortie connue.
    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; init; }

    // Échelle 0-100 côté IGDB (contrairement à TMDB, échelle 0-10) — volontairement non normalisée,
    // voir plans/active-plan.md (edge cases).
    [JsonPropertyName("total_rating")]
    public double? TotalRating { get; init; }

    [JsonPropertyName("total_rating_count")]
    public int? TotalRatingCount { get; init; }

    [JsonPropertyName("genres")]
    public List<int> Genres { get; init; } = [];

    [JsonPropertyName("cover")]
    public IgdbCoverDto? Cover { get; init; }
}

public sealed class IgdbCoverDto
{
    [JsonPropertyName("image_id")]
    public string? ImageId { get; init; }
}
