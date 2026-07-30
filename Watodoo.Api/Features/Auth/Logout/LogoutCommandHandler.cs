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

        var existing = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.Token == command.RefreshToken, ct);
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
