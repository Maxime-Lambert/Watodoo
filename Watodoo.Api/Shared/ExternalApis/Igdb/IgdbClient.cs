using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;

namespace Watodoo.Shared.ExternalApis.Igdb;

public sealed class IgdbClient(HttpClient httpClient, IIgdbTokenProvider tokenProvider, IOptions<IgdbOptions> options) : IIgdbClient
{
    private const string Fields =
        "id,name,summary,first_release_date,total_rating,total_rating_count,genres,cover.image_id";

    public async Task<List<IgdbGameDto>> DiscoverGamesAsync(IgdbQuery query, CancellationToken ct)
    {
        var body = BuildApicalypseBody(query);

        var response = await SendAsync(body, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Token révoqué avant son expiration théorique, ou expiré malgré la marge de sécurité du
            // provider : un seul retry après invalidation forcée, pas de boucle infinie.
            await tokenProvider.InvalidateAsync(ct);
            response = await SendAsync(body, ct);
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<IgdbGameDto>>(ct) ?? [];
    }

    private async Task<HttpResponseMessage> SendAsync(string body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "games")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Client-ID", options.Value.ClientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", await tokenProvider.GetAccessTokenAsync(ct));

        return await httpClient.SendAsync(request, ct);
    }

    private static string BuildApicalypseBody(IgdbQuery query)
    {
        var body = $"fields {Fields}; sort {query.SortBy}; limit {query.Limit}; offset {query.Offset};";

        if (query.ReleaseDateFrom is null && query.ReleaseDateTo is null)
        {
            return body;
        }

        var conditions = new List<string>();
        if (query.ReleaseDateFrom is { } from)
        {
            conditions.Add($"first_release_date >= {ToUnixSeconds(from)}");
        }

        if (query.ReleaseDateTo is { } to)
        {
            conditions.Add($"first_release_date <= {ToUnixSeconds(to)}");
        }

        return $"{body} where {string.Join(" & ", conditions)};";
    }

    private static long ToUnixSeconds(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
