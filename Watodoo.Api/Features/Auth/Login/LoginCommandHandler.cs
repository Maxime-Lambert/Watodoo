using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;

namespace Watodoo.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IValidator<LoginCommand> validator,
    UserManager<ApplicationUser> userManager,
    WatodooDbContext db,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new Watodoo.Shared.Exceptions.ValidationException(
                (IReadOnlyDictionary<string, string[]>)validation.ToDictionary());
        }

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, command.Password))
        {
            throw new UnauthorizedException("Email ou mot de passe incorrect.");
        }

        var accessToken = tokenGenerator.GenerateAccessToken(user);
        var refreshTokenValue = JwtTokenGenerator.GenerateRefreshTokenValue();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenPolicy.LifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshTokenValue,
            UserId = user.Id,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResult(user.Id, user.Email!, accessToken, refreshTokenValue, expiresAt);
    }
}
