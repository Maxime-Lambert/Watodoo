using Microsoft.AspNetCore.RateLimiting;
using Watodoo.Features.Auth.Login;
using Watodoo.Features.Auth.Logout;
using Watodoo.Features.Auth.Me;
using Watodoo.Features.Auth.Refresh;
using Watodoo.Features.Auth.Register;

namespace Watodoo.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").RequireRateLimiting("auth");

        RegisterEndpoint.Map(group);
        LoginEndpoint.Map(group);
        RefreshEndpoint.Map(group);
        LogoutEndpoint.Map(group);
        GetMeEndpoint.Map(group);
    }
}
