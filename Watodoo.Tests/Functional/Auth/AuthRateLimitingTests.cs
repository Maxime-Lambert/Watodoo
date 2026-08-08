using System.Net;
using System.Net.Http.Json;
using Watodoo.Features.Auth.Login;

namespace Watodoo.Tests.Functional.Auth;

[Collection(nameof(AuthRateLimitingTestCollection))]
public sealed class AuthRateLimitingTests(AuthRateLimitingTestFixture fixture)
{
    // Permis à mauvais escient volontairement : le rate limiter s'exécute avant le handler,
    // donc un login raté consomme le quota tout aussi bien qu'un login réussi, sans avoir à
    // créer de compte au préalable.
    private static Task<HttpResponseMessage> LoginAttempt(HttpClient client, string? forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new LoginCommand("nobody@example.com", "wrong-password")),
        };

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task Exceeding_the_limit_for_one_ip_returns_429_only_past_the_permit_limit()
    {
        var client = fixture.Factory.CreateClient();
        const string ip = "203.0.113.10";

        var first = await LoginAttempt(client, ip);
        var second = await LoginAttempt(client, ip);
        var third = await LoginAttempt(client, ip);
        var fourth = await LoginAttempt(client, ip);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }

    [Fact]
    public async Task Two_different_ips_have_independent_quotas()
    {
        var client = fixture.Factory.CreateClient();
        const string ipA = "203.0.113.20";
        const string ipB = "203.0.113.21";

        for (var i = 0; i < 3; i++)
        {
            await LoginAttempt(client, ipA);
        }

        var blockedForA = await LoginAttempt(client, ipA);
        var stillOkForB = await LoginAttempt(client, ipB);

        Assert.Equal(HttpStatusCode.TooManyRequests, blockedForA.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stillOkForB.StatusCode);
    }

    [Fact]
    public async Task Forwarded_chain_uses_the_leftmost_original_client_ip_not_the_last_hop()
    {
        var client = fixture.Factory.CreateClient();
        const string sharedEdgeIp = "198.51.100.1"; // simule l'IP Cloudflare, partagée par tout le monde
        const string clientA = "203.0.113.30";
        const string clientB = "203.0.113.31";

        for (var i = 0; i < 3; i++)
        {
            await LoginAttempt(client, $"{clientA}, {sharedEdgeIp}");
        }

        var blockedForClientA = await LoginAttempt(client, $"{clientA}, {sharedEdgeIp}");
        var stillOkForClientB = await LoginAttempt(client, $"{clientB}, {sharedEdgeIp}");

        Assert.Equal(HttpStatusCode.TooManyRequests, blockedForClientA.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stillOkForClientB.StatusCode);
    }

    [Fact]
    public async Task Forged_leftmost_entry_does_not_bypass_the_real_client_partition()
    {
        var client = fixture.Factory.CreateClient();
        // ForwardLimit=2 : seules les 2 entrées les plus à droite (posées par Cloudflare et
        // Caddy, jamais par le client) comptent. Un client qui préfixe une IP différente à
        // chaque requête ne doit donc pas pouvoir échapper à son propre quota.
        const string realClientIp = "203.0.113.40";
        const string cloudflareIp = "172.68.0.1";

        var first = await LoginAttempt(client, $"10.0.0.1, {realClientIp}, {cloudflareIp}");
        var second = await LoginAttempt(client, $"10.0.0.2, {realClientIp}, {cloudflareIp}");
        var third = await LoginAttempt(client, $"10.0.0.3, {realClientIp}, {cloudflareIp}");
        var fourth = await LoginAttempt(client, $"10.0.0.4, {realClientIp}, {cloudflareIp}");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }

    [Fact]
    public async Task Missing_forwarded_header_does_not_error_and_still_gets_rate_limited()
    {
        var client = fixture.Factory.CreateClient();

        var first = await LoginAttempt(client, forwardedFor: null);
        var second = await LoginAttempt(client, forwardedFor: null);
        var third = await LoginAttempt(client, forwardedFor: null);
        var fourth = await LoginAttempt(client, forwardedFor: null);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }
}
