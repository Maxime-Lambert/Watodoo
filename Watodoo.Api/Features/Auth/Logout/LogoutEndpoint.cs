namespace Watodoo.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/logout", async (
            HttpRequest request,
            HttpResponse response,
            LogoutCommandHandler handler,
            CancellationToken ct) =>
        {
            var token = AuthCookies.GetRefreshTokenCookie(request);
            await handler.Handle(new LogoutCommand(token), ct);
            AuthCookies.ClearRefreshTokenCookie(response);
            return Results.NoContent();
        });
    }
}
