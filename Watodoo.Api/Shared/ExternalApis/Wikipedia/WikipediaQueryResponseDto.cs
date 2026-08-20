using System.Text.Json.Serialization;

namespace Watodoo.Shared.ExternalApis.Wikipedia;

public sealed class WikipediaQueryResponseDto
{
    [JsonPropertyName("query")]
    public WikipediaQueryDto? Query { get; init; }
}

public sealed class WikipediaQueryDto
{
    // Titres réécrits par normalisation Unicode (espaces/soulignés, casse...) avant résolution.
    [JsonPropertyName("normalized")]
    public List<WikipediaTitleMappingDto> Normalized { get; init; } = [];

    // Titres réécrits par suivi de redirection (redirects=1 dans la requête).
    [JsonPropertyName("redirects")]
    public List<WikipediaTitleMappingDto> Redirects { get; init; } = [];

    // Nécessite &formatversion=2 dans la requête : "pages" devient une liste (pas un dictionnaire
    // par pageid) et "missing" un vrai booléen — évite l'ambiguïté du formatversion=1 par défaut de
    // MediaWiki (attribut vide plutôt qu'un booléen explicite).
    [JsonPropertyName("pages")]
    public List<WikipediaPageDto> Pages { get; init; } = [];
}

public sealed class WikipediaTitleMappingDto
{
    [JsonPropertyName("from")]
    public string From { get; init; } = "";

    [JsonPropertyName("to")]
    public string To { get; init; } = "";
}

public sealed class WikipediaPageDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("extract")]
    public string? Extract { get; init; }

    [JsonPropertyName("missing")]
    public bool Missing { get; init; }
}
