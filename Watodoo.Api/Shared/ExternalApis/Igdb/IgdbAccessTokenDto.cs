using System.Text.Json.Serialization;

namespace Watodoo.Shared.ExternalApis.Igdb;

public sealed class IgdbAccessTokenDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; init; }
}
