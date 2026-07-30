using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Watodoo.Features.Auth.Login;
using Watodoo.Features.Auth.Me;
using Watodoo.Features.Auth.Register;

namespace Watodoo.Tests.Functional.Auth;

[Collection(nameof(FunctionalTestCollection))]
public sealed class AuthEndpointsTests(FunctionalTestFixture fixture)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    // HandleCookies=false : ces tests posent des cookies "Cookie" explicites sur chaque requête
    // (y compris volontairement des cookies périmés/révoqués) — le CookieContainer par défaut de
    // WebApplicationFactory intercepterait et remplacerait ces valeurs par le dernier Set-Cookie reçu.
    private HttpClient CreateClient() =>
        fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string? ExtractCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        return values.First().Split(';').First();
    }

    [Fact]
    public async Task Register_creates_account_and_sets_refresh_cookie()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(UniqueEmail(), "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(ExtractCookieHeader(response));
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
    }

    [Fact]
    public async Task Register_with_existing_email_returns_conflict()
    {
        var client = CreateClient();
        var email = UniqueEmail();

        await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));
        var second = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_ok_and_sets_refresh_cookie()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));

        var response = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, "Passw0rd1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(ExtractCookieHeader(response));
    }

    [Fact]
    public async Task Login_with_invalid_password_returns_unauthorized()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));

        var response = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_token_returns_current_user()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Authorization", $"Bearer {registered!.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<GetMeResponse>();
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_invalidates_the_previous_token()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));
        var firstCookie = ExtractCookieHeader(registerResponse)!;

        var firstRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        firstRefreshRequest.Headers.Add("Cookie", firstCookie);
        var firstRefreshResponse = await client.SendAsync(firstRefreshRequest);

        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);
        var secondCookie = ExtractCookieHeader(firstRefreshResponse)!;
        Assert.NotEqual(firstCookie, secondCookie);

        var reuseRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        reuseRequest.Headers.Add("Cookie", firstCookie);
        var reuseResponse = await client.SendAsync(reuseRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var secondRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        secondRefreshRequest.Headers.Add("Cookie", secondCookie);
        var secondRefreshResponse = await client.SendAsync(secondRefreshRequest);

        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_then_refresh_fails()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, "Passw0rd1"));
        var cookie = ExtractCookieHeader(registerResponse)!;

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookie);
        var logoutResponse = await client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookie);
        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_without_cookie_is_idempotent()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
