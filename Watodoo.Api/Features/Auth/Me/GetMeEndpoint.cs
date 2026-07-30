using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Watodoo.Shared.Exceptions;

namespace Watodoo.Features.Auth.Me;

public static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (ClaimsPrincipal principal, GetMeQueryHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            {
                throw new UnauthorizedException("Token invalide.");
            }

            var result = await handler.Handle(new GetMeQuery(userId), ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
