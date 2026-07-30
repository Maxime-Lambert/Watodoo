using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;

namespace Watodoo.Features.Auth.Refresh;

public sealed class RefreshCommandHandler(
    WatodooDbContext db,
    UserManager<ApplicationUser> userManager,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(RefreshCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            throw new UnauthorizedException("Refresh token invalide ou expiré.");
        }

        var hashedToken = JwtTokenGenerator.HashRefreshTokenValue(command.RefreshToken);

        var existing = await db.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        if (existing is null || !existing.IsActive)
        {
            throw new UnauthorizedException("Refresh token invalide ou expiré.");
        }

        // Révocation atomique conditionnée par l'état encore actif : élimine la
        // fenêtre de course où deux refresh concurrents avec le même token
        // produiraient chacun un nouveau token valide.
        var revokedRows = await db.RefreshTokens
            .Where(rt => rt.Id == existing.Id && rt.RevokedAt == null && rt.ExpiresAt > DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.RevokedAt, DateTimeOffset.UtcNow), ct);

        if (revokedRows == 0)
        {
            throw new UnauthorizedException("Refresh token invalide ou expiré.");
        }

        var user = await userManager.FindByIdAsync(existing.UserId.ToString())
            ?? throw new UnauthorizedException("Refresh token invalide ou expiré.");

        return await tokenGenerator.IssueTokenPairAsync(user, db, ct);
    }
}
