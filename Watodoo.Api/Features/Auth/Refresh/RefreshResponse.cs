namespace Watodoo.Features.Auth.Refresh;

public sealed record RefreshResponse(Guid UserId, string Email, string AccessToken);
