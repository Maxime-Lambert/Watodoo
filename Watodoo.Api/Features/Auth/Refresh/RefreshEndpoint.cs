namespace Watodoo.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/refresh", async (
            HttpRequest request,
            HttpResponse response,
            RefreshCommandHandler handler,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var token = AuthCookies.GetRefreshTokenCookie(request) ?? string.Empty;
            var result = await handler.Handle(new RefreshCommand(token), ct);
            AuthCookies.SetRefreshTokenCookie(response, result.RefreshToken, result.RefreshTokenExpiresAt, env.IsDevelopment());
            return Results.Ok(new RefreshResponse(result.UserId, result.Email, result.AccessToken));
        });
    }
}
