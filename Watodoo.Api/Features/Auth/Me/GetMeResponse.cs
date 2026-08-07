namespace Watodoo.Features.Auth.Me;

public sealed record GetMeResponse(Guid UserId, string Email);
