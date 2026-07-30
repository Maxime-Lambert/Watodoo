using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;

namespace Watodoo.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IValidator<RegisterCommand> validator,
    UserManager<ApplicationUser> userManager,
    WatodooDbContext db,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(RegisterCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new Watodoo.Shared.Exceptions.ValidationException(
                (IReadOnlyDictionary<string, string[]>)validation.ToDictionary());
        }

        var existing = await userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
        {
            throw new ConflictException("Un compte existe déjà avec cet email.");
        }

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Échec inattendu de la création de compte : {string.Join(", ", result.Errors.Select(e => e.Description))}");
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
