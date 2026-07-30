namespace Watodoo.Features.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; init; }

    public required string Token { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
