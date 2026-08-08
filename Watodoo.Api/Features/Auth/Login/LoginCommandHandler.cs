using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;
using Watodoo.Shared.Validation;

namespace Watodoo.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IValidator<LoginCommand> validator,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    WatodooDbContext db,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowCustomAsync(command, ct);

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            throw new UnauthorizedException("Email ou mot de passe incorrect.");
        }

        // lockoutOnFailure: true incrémente le compteur d'échecs et verrouille le compte après
        // le seuil configuré (Lockout:MaxFailedAccessAttempts). Message générique même en cas de
        // verrouillage : un email inexistant ne peut jamais atteindre cet état (sortie anticipée
        // ci-dessus), donc distinguer "verrouillé" de "identifiants invalides" permettrait à un
        // attaquant de savoir qu'un email correspond à un compte réel après quelques tentatives.
        var result = await signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Email ou mot de passe incorrect.");
        }

        return await tokenGenerator.IssueTokenPairAsync(user, db, ct);
    }
}
