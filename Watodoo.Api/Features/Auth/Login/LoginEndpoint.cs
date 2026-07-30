namespace Watodoo.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/login", async (
            LoginCommand command,
            LoginCommandHandler handler,
            HttpResponse response,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(command, ct);
            AuthCookies.SetRefreshTokenCookie(response, result.RefreshToken, result.RefreshTokenExpiresAt, env.IsDevelopment());
            return Results.Ok(new LoginResponse(result.UserId, result.Email, result.AccessToken));
        });
    }
}
