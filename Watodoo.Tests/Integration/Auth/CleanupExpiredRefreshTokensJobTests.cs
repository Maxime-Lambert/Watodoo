using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Watodoo.Features.Auth;
using Watodoo.Features.Auth.CleanupExpiredRefreshTokens;
using Watodoo.Shared.Data;

namespace Watodoo.Tests.Integration.Auth;

// Pas d'assertion sur le nombre de lignes supprimées : la fixture Postgres est partagée entre
// tous les tests de la collection (cf. AuthDbCollection), donc le job peut aussi supprimer des
// lignes mortes laissées par d'autres tests (ex: RefreshTokenRotationTests révoque un token sans
// le nettoyer). Chaque test vérifie uniquement la présence/absence de son propre token.
[Collection(nameof(AuthDbCollection))]
public sealed class CleanupExpiredRefreshTokensJobTests(AuthDbFixture fixture)
{
    private static async Task<ApplicationUser> CreateUser(WatodooDbContext context, string email)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Deletes_expired_non_revoked_tokens()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context, "cleanup-expired@example.com");
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "cleanup-expired-token",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await context.SaveChangesAsync();

        await new CleanupExpiredRefreshTokensJob(context, NullLogger<CleanupExpiredRefreshTokensJob>.Instance).RunAsync();

        await using var verifyContext = fixture.CreateContext();
        Assert.False(await verifyContext.RefreshTokens.AnyAsync(rt => rt.Token == "cleanup-expired-token"));
    }

    [Fact]
    public async Task Deletes_revoked_tokens_even_if_not_yet_expired()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context, "cleanup-revoked@example.com");
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "cleanup-revoked-token",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
            RevokedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        await new CleanupExpiredRefreshTokensJob(context, NullLogger<CleanupExpiredRefreshTokensJob>.Instance).RunAsync();

        await using var verifyContext = fixture.CreateContext();
        Assert.False(await verifyContext.RefreshTokens.AnyAsync(rt => rt.Token == "cleanup-revoked-token"));
    }

    [Fact]
    public async Task Keeps_active_tokens()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context, "cleanup-active@example.com");
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "cleanup-active-token",
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
        });
        await context.SaveChangesAsync();

        await new CleanupExpiredRefreshTokensJob(context, NullLogger<CleanupExpiredRefreshTokensJob>.Instance).RunAsync();

        await using var verifyContext = fixture.CreateContext();
        Assert.True(await verifyContext.RefreshTokens.AnyAsync(rt => rt.Token == "cleanup-active-token"));
    }

    [Fact]
    public async Task Runs_without_throwing_regardless_of_table_state()
    {
        await using var context = fixture.CreateContext();

        var deletedCount = await new CleanupExpiredRefreshTokensJob(context, NullLogger<CleanupExpiredRefreshTokensJob>.Instance).RunAsync();

        Assert.True(deletedCount >= 0);
    }
}
