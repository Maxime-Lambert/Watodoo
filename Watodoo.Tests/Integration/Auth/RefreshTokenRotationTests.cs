using Microsoft.EntityFrameworkCore;
using Watodoo.Features.Auth;

namespace Watodoo.Tests.Integration.Auth;

[Collection(nameof(AuthDbCollection))]
public sealed class RefreshTokenRotationTests(AuthDbFixture fixture)
{
    [Fact]
    public async Task Rotating_a_refresh_token_revokes_the_old_one_and_creates_a_new_one()
    {
        await using var context = fixture.CreateContext();

        var user = new ApplicationUser { UserName = "rotate@example.com", Email = "rotate@example.com" };
        context.Users.Add(user);

        var oldToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "rotation-old-token",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
        };
        context.RefreshTokens.Add(oldToken);
        await context.SaveChangesAsync();

        oldToken.RevokedAt = DateTimeOffset.UtcNow;
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "rotation-new-token",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
        });
        await context.SaveChangesAsync();

        await using var verifyContext = fixture.CreateContext();
        var reloadedOld = await verifyContext.RefreshTokens.SingleAsync(rt => rt.Token == "rotation-old-token");
        var reloadedNew = await verifyContext.RefreshTokens.SingleAsync(rt => rt.Token == "rotation-new-token");

        Assert.NotNull(reloadedOld.RevokedAt);
        Assert.False(reloadedOld.IsActive);
        Assert.Null(reloadedNew.RevokedAt);
        Assert.True(reloadedNew.IsActive);
    }

    [Fact]
    public async Task Duplicate_token_value_violates_the_unique_constraint()
    {
        await using var context = fixture.CreateContext();

        var user = new ApplicationUser { UserName = "duplicate@example.com", Email = "duplicate@example.com" };
        context.Users.Add(user);
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "duplicate-token-value",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
        });
        await context.SaveChangesAsync();

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "duplicate-token-value",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
