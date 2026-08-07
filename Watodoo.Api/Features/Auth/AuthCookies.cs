using Microsoft.AspNetCore.Http;

namespace Watodoo.Features.Auth;

public static class AuthCookies
{
    private const string CookieName = "refreshToken";

    public static void SetRefreshTokenCookie(HttpResponse response, string token, DateTimeOffset expiresAt, bool isDevelopment)
    {
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            Expires = expiresAt,
        });
    }

    public static string? GetRefreshTokenCookie(HttpRequest request) => request.Cookies[CookieName];

    public static void ClearRefreshTokenCookie(HttpResponse response) =>
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/auth" });
}
