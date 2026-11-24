using System.Text.Json.Serialization;
using ChatFusion.Identity.Api.Infrastructure.Security;

namespace ChatFusion.Identity.Api.Contracts;

/// <summary>Набор открытых ключей (JWKS, RFC 7517).</summary>
/// <param name="Keys">Открытые ключи для проверки подписи токенов.</param>
public sealed record JsonWebKeySetResponse(
    [property: JsonPropertyName("keys")] IReadOnlyList<PublicJsonWebKey> Keys);

/// <summary>Метаданные издателя токенов в формате OpenID Connect Discovery.</summary>
public sealed record OpenIdConfigurationResponse(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")] IReadOnlyList<string> SigningAlgorithms,
    [property: JsonPropertyName("claims_supported")] IReadOnlyList<string> ClaimsSupported);
