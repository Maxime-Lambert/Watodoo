using Microsoft.AspNetCore.Identity;
using Watodoo.Shared.Exceptions;

namespace Watodoo.Features.Auth.Me;

public sealed class GetMeQueryHandler(UserManager<ApplicationUser> userManager)
{
    public async Task<GetMeResponse> Handle(GetMeQuery query, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(query.UserId.ToString())
            ?? throw new NotFoundException("Utilisateur introuvable.");

        return new GetMeResponse(user.Id, user.Email!);
    }
}
