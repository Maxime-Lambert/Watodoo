using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Watodoo.Features.Auth.Me;

public static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me", async (ClaimsPrincipal principal, GetMeQueryHandler handler, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var result = await handler.Handle(new GetMeQuery(userId), ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
