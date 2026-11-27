using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Infrastructure.Security;

/// <summary>
/// Создание refresh-токенов: 32 случайных байта в Base64Url. В БД хранится только SHA-256.
/// </summary>
public static class RefreshTokenGenerator
{
    private const int TokenSize = 32;

    /// <summary>Длиннее этого значения токен заведомо не наш и не хешируется.</summary>
    public const int MaxTokenLength = 128;

    public static string Generate() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(TokenSize));

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
