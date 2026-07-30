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
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.Token == command.RefreshToken, ct);
        if (existing is null || !existing.IsActive)
        {
            throw new UnauthorizedException("Refresh token invalide ou expiré.");
        }

        var user = await userManager.FindByIdAsync(existing.UserId.ToString());
        if (user is null)
        {
            throw new UnauthorizedException("Refresh token invalide ou expiré.");
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;

        var accessToken = tokenGenerator.GenerateAccessToken(user);
        var newRefreshTokenValue = JwtTokenGenerator.GenerateRefreshTokenValue();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenPolicy.LifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = newRefreshTokenValue,
            UserId = user.Id,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResult(user.Id, user.Email!, accessToken, newRefreshTokenValue, expiresAt);
    }
}
