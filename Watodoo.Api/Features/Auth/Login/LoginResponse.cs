namespace Watodoo.Features.Auth.Login;

public sealed record LoginResponse(Guid UserId, string Email, string AccessToken);
