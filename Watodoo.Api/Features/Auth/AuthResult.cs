namespace Watodoo.Features.Auth;

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
