namespace Watodoo.Shared.ExternalApis.Igdb;

public interface IIgdbTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct);

    // Force un rafraîchissement au prochain appel : utilisé par IgdbClient quand IGDB répond 401
    // (token révoqué avant son expiration théorique, ou expiré malgré la marge de sécurité).
    Task InvalidateAsync(CancellationToken ct);
}
