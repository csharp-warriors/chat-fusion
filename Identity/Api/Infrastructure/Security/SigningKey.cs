using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Infrastructure.Security;

/// <summary>
/// Ключ RSA для подписи и проверки access-токенов.
/// </summary>
public sealed class SigningKey
{
    public const int MinKeySize = 2048;
    public const int GeneratedKeySize = 3072;

    private readonly RSAParameters _publicParameters;

    private SigningKey(RSA rsa, bool hasPrivateKey)
    {
        _publicParameters = rsa.ExportParameters(includePrivateParameters: false);

        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_publicParameters));
        KeyId = Base64UrlEncoder.Encode(publicJwk.ComputeJwkThumbprint());

        SecurityKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        HasPrivateKey = hasPrivateKey;
    }

    /// <summary>Идентификатор ключа (<c>kid</c>): отпечаток открытого ключа по RFC 7638.</summary>
    public string KeyId { get; }

    public RsaSecurityKey SecurityKey { get; }

    public bool HasPrivateKey { get; }

    public SigningCredentials CreateSigningCredentials() =>
        HasPrivateKey
            ? new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256)
            : throw new InvalidOperationException("Ключ без закрытой части не может подписывать токены.");

    /// <summary>Открытая часть ключа в виде JWK для публикации в JWKS.</summary>
    public PublicJsonWebKey ToPublicJwk() => new(
        Kty: "RSA",
        Use: "sig",
        Alg: SecurityAlgorithms.RsaSha256,
        Kid: KeyId,
        N: Base64UrlEncoder.Encode(_publicParameters.Modulus),
        E: Base64UrlEncoder.Encode(_publicParameters.Exponent));

    public static SigningKey Generate()
    {
        var rsa = RSA.Create(MinKeySize);
        return new SigningKey(rsa, hasPrivateKey: true);
    }

    /// <summary>
    /// Загружает ключ из PEM-файла.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <param name="requirePrivateKey">Требовать закрытую часть ключа (нужна для подписи).</param>
    public static SigningKey LoadFromPemFile(string path, bool requirePrivateKey)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Файл ключа подписи не найден: {path}");
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            rsa.Dispose();
            throw new InvalidOperationException($"Файл {path} не содержит ключ RSA в формате PEM.", exception);
        }

        if (rsa.KeySize < MinKeySize)
        {
            rsa.Dispose();
            throw new InvalidOperationException($"Длина ключа подписи должна быть не меньше {MinKeySize} бит.");
        }

        var hasPrivateKey = HasPrivatePart(rsa);
        if (requirePrivateKey && !hasPrivateKey)
        {
            rsa.Dispose();
            throw new InvalidOperationException($"Файл {path} должен содержать закрытый ключ RSA.");
        }

        return new SigningKey(rsa, hasPrivateKey);
    }

    /// <summary>
    /// Создаёт новый закрытый ключ RSA в формате PEM (PKCS#8).
    /// </summary>
    public static string GeneratePrivateKeyPem()
    {
        using var rsa = RSA.Create(GeneratedKeySize);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private static bool HasPrivatePart(RSA rsa)
    {
        try
        {
            rsa.ExportParameters(includePrivateParameters: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

/// <summary>
/// Открытый ключ в формате JWK (RFC 7517).
/// </summary>
public sealed record PublicJsonWebKey(string Kty, string Use, string Alg, string Kid, string N, string E);
