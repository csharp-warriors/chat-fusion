using ChatFusion.Identity.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Infrastructure.Security;

/// <summary>
/// Имена claims в токенах сервиса.
/// </summary>
public static class IdentityClaimTypes
{
    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string TokenId = "jti";
    public const string Role = "role";
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Выпуск access-токенов.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, Guid sessionId, IReadOnlyCollection<string> roles);
}

/// <summary>
/// Выпускает JWT с подписью RS256. Персональные данные в токен не попадают.
/// </summary>
public sealed class JwtTokenService(
    SigningKeyProvider keys,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken CreateAccessToken(Guid userId, Guid sessionId, IReadOnlyCollection<string> roles)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + jwt.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = keys.Current.CreateSigningCredentials(),
            Claims = new Dictionary<string, object>
            {
                [IdentityClaimTypes.Subject] = userId.ToString(),
                [IdentityClaimTypes.SessionId] = sessionId.ToString(),
                [IdentityClaimTypes.TokenId] = Guid.CreateVersion7().ToString(),
                [IdentityClaimTypes.Role] = roles.ToArray(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
