namespace Watodoo.Features.Auth.Register;

public sealed record RegisterResponse(Guid UserId, string Email, string AccessToken);
