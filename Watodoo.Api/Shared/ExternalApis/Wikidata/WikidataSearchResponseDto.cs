using System.Text.Json.Serialization;

namespace Watodoo.Shared.ExternalApis.Wikidata;

// Réponse de action=wbsearchentities.
public sealed class WikidataSearchResponseDto
{
    [JsonPropertyName("search")]
    public List<WikidataSearchResultDto> Search { get; init; } = [];
}

public sealed class WikidataSearchResultDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
}
