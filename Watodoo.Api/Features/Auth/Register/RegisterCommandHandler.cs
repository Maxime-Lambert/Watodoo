using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Data;
using Watodoo.Shared.Exceptions;
using Watodoo.Shared.Validation;

namespace Watodoo.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IValidator<RegisterCommand> validator,
    UserManager<ApplicationUser> userManager,
    WatodooDbContext db,
    JwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> Handle(RegisterCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowCustomAsync(command, ct);

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

        return await tokenGenerator.IssueTokenPairAsync(user, db, ct);
    }
}
