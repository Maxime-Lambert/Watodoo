using System.Text.Json;
using System.Text.Json.Serialization;

namespace Watodoo.Shared.ExternalApis.Wikidata;

// Réponse de action=wbgetentities&props=labels|claims|sitelinks.
public sealed class WikidataEntitiesResponseDto
{
    [JsonPropertyName("entities")]
    public Dictionary<string, WikidataEntityDto> Entities { get; init; } = [];
}

public sealed class WikidataEntityDto
{
    [JsonPropertyName("labels")]
    public Dictionary<string, WikidataLabelDto> Labels { get; init; } = [];

    [JsonPropertyName("claims")]
    public Dictionary<string, List<WikidataClaimDto>> Claims { get; init; } = [];

    [JsonPropertyName("sitelinks")]
    public Dictionary<string, WikidataSitelinkDto> Sitelinks { get; init; } = [];
}

public sealed class WikidataLabelDto
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = "";
}

public sealed class WikidataSitelinkDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";
}

public sealed class WikidataClaimDto
{
    [JsonPropertyName("mainsnak")]
    public WikidataSnakDto? MainSnak { get; init; }
}

public sealed class WikidataSnakDto
{
    [JsonPropertyName("datavalue")]
    public WikidataDataValueDto? DataValue { get; init; }
}

// La forme de "value" dépend du datatype de la propriété (wikibase-item pour P31, time pour P577) :
// laissé en JsonElement brut, interprété au cas par cas par l'appelant plutôt que de modéliser
// chaque variante.
public sealed class WikidataDataValueDto
{
    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }
}
