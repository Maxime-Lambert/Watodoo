namespace Watodoo.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/register", async (
            RegisterCommand command,
            RegisterCommandHandler handler,
            HttpResponse response,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(command, ct);
            AuthCookies.SetRefreshTokenCookie(response, result.RefreshToken, result.RefreshTokenExpiresAt, env.IsDevelopment());
            return Results.Created("/auth/me", new RegisterResponse(result.UserId, result.Email, result.AccessToken));
        });
    }
}
