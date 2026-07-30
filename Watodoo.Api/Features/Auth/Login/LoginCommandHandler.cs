using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;
using Watodoo.Shared.Validation;

namespace Watodoo.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IValidator<LoginCommand> validator,
    UserManager<ApplicationUser> userManager,
    WatodooDbContext db,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowCustomAsync(command, ct);

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, command.Password))
        {
            throw new UnauthorizedException("Email ou mot de passe incorrect.");
        }

        return await tokenGenerator.IssueTokenPairAsync(user, db, ct);
    }
}
