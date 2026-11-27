using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Публичные ключи и метаданные для проверки токенов другими сервисами.
/// </summary>
[Route(".well-known")]
[AllowAnonymous]
[DisableRateLimiting]
public sealed class DiscoveryController(SigningKeyProvider keys, IOptions<JwtOptions> jwtOptions) : ApiControllerBase
{
    private const int CacheDurationSeconds = 300;

    /// <summary>
    /// Возвращает открытые ключи (JWKS): текущий и, на время ротации, предыдущий.
    /// </summary>
    /// <response code="200">Набор ключей.</response>
    [HttpGet("jwks.json")]
    [ResponseCache(Duration = CacheDurationSeconds, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType<JsonWebKeySetResponse>(StatusCodes.Status200OK)]
    public IActionResult GetJsonWebKeySet() =>
        Ok(new JsonWebKeySetResponse(keys.ValidationKeys.Select(k => k.ToPublicJwk()).ToList()));

    /// <summary>
    /// Возвращает метаданные издателя. Адрес JWKS строится от адреса запроса,
    /// поэтому сервисы внутри сети Docker получают внутренний адрес.
    /// </summary>
    /// <response code="200">Метаданные.</response>
    [HttpGet("openid-configuration")]
    [ResponseCache(Duration = CacheDurationSeconds, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType<OpenIdConfigurationResponse>(StatusCodes.Status200OK)]
    public IActionResult GetOpenIdConfiguration()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        return Ok(new OpenIdConfigurationResponse(
            Issuer: jwtOptions.Value.Issuer,
            JwksUri: $"{baseUrl}/.well-known/jwks.json",
            SigningAlgorithms: [SecurityAlgorithms.RsaSha256],
            ClaimsSupported:
            [
                IdentityClaimTypes.Subject,
                IdentityClaimTypes.SessionId,
                IdentityClaimTypes.TokenId,
                IdentityClaimTypes.Role,
            ]));
    }
}
