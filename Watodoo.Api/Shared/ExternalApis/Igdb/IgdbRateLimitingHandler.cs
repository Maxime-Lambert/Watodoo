namespace Watodoo.Shared.ExternalApis.Igdb;

// IGDB documente une limite de 4 requêtes/seconde. Verrou statique partagé (pas d'instance par
// requête, ce handler est enregistré Transient sur le HttpClient IGDB via AddHttpMessageHandler) :
// c'est bien l'appel réseau global vers IGDB qu'il faut espacer, pas un compteur par instance.
public sealed class IgdbRateLimitingHandler : DelegatingHandler
{
    // 260ms plutôt que 250ms pile : marge de sécurité sous la limite documentée de 4 req/s.
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(260);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var elapsedSinceLastRequest = DateTimeOffset.UtcNow - _lastRequestAt;
            if (elapsedSinceLastRequest < MinInterval)
            {
                await Task.Delay(MinInterval - elapsedSinceLastRequest, ct);
            }

            _lastRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            Gate.Release();
        }

        return await base.SendAsync(request, ct);
    }
}
