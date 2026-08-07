using Microsoft.EntityFrameworkCore;
using Watodoo.Shared.Data;

namespace Watodoo.Features.Auth.Logout;

public sealed class LogoutCommandHandler(WatodooDbContext db)
{
    public async Task Handle(LogoutCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return;
        }

        var hashedToken = JwtTokenGenerator.HashRefreshTokenValue(command.RefreshToken);

        await db.RefreshTokens
            .Where(rt => rt.Token == hashedToken && rt.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.RevokedAt, DateTimeOffset.UtcNow), ct);
    }
}
