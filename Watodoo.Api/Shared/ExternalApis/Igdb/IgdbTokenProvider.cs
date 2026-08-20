using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;

namespace Watodoo.Shared.ExternalApis.Igdb;

// AddHttpClient<TClient, TImplementation> enregistre TImplementation en Transient (une nouvelle
// instance à chaque résolution) — un champ d'instance ne serait donc jamais partagé entre deux jobs.
// Le cache est délibérément statique pour rester partagé par tout le process quel que soit le nombre
// d'instances créées : il n'y a rien de spécifique à un utilisateur ou une requête HTTP entrante dans
// un token client-credentials, donc pas de raison de le refaire à chaque résolution. Même pattern que
// IgdbRateLimitingHandler (état statique partagé entre instances Transient).
public sealed class IgdbTokenProvider(HttpClient httpClient, IOptions<IgdbOptions> options) : IIgdbTokenProvider
{
    // Marge de sécurité avant l'expiration théorique : évite qu'un token jugé valide au moment de la
    // vérification expire réellement pendant le trajet réseau vers IGDB.
    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromMinutes(5);

    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static string? _cachedToken;
    private static DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is { } token && DateTimeOffset.UtcNow < _expiresAt)
        {
            return token;
        }

        await Lock.WaitAsync(ct);
        try
        {
            // Un autre appelant a peut-être déjà rafraîchi pendant l'attente du verrou.
            if (_cachedToken is { } refreshedToken && DateTimeOffset.UtcNow < _expiresAt)
            {
                return refreshedToken;
            }

            // En corps de requête (form-urlencoded), pas en query string : RFC 6749 §2.3.1, évite
            // aussi que le secret se retrouve dans l'URL loguée par des intermédiaires réseau (proxy,
            // CDN...) qui ne redactent pas forcément la query string comme le fait par défaut le
            // logging HttpClientFactory de ce projet.
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.Value.ClientId,
                ["client_secret"] = options.Value.ClientSecret,
                ["grant_type"] = "client_credentials",
            });
            var response = await httpClient.PostAsync("oauth2/token", body, ct);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<IgdbAccessTokenDto>(ct)
                ?? throw new InvalidOperationException("Réponse IGDB/Twitch OAuth2 vide.");

            _cachedToken = dto.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(dto.ExpiresInSeconds) - ExpirySafetyMargin;

            return _cachedToken;
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task InvalidateAsync(CancellationToken ct)
    {
        await Lock.WaitAsync(ct);
        try
        {
            _cachedToken = null;
            _expiresAt = DateTimeOffset.MinValue;
        }
        finally
        {
            Lock.Release();
        }
    }
}
