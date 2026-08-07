using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Watodoo.Configuration;
using Watodoo.Shared.Data;

namespace Watodoo.Features.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string GenerateAccessToken(ApplicationUser user)
    {
        var email = user.Email ?? throw new InvalidOperationException("Utilisateur sans email.");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<AuthResult> IssueTokenPairAsync(ApplicationUser user, WatodooDbContext db, CancellationToken ct)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshTokenValue = GenerateRefreshTokenValue();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenPolicy.LifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshTokenValue(refreshTokenValue),
            UserId = user.Id,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResult(
            user.Id,
            user.Email ?? throw new InvalidOperationException("Utilisateur sans email."),
            accessToken,
            refreshTokenValue,
            expiresAt);
    }

    public static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string HashRefreshTokenValue(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
