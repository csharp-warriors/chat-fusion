using ChatFusion.Identity.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.Api.Infrastructure.Security;

/// <summary>
/// Текущий ключ подписи и ключи, по которым проверяются токены.
/// </summary>
public sealed class SigningKeyProvider
{
    public SigningKeyProvider(
        IOptions<JwtOptions> options,
        IHostEnvironment environment,
        ILogger<SigningKeyProvider> logger)
    {
        var jwt = options.Value;

        if (string.IsNullOrWhiteSpace(jwt.SigningKeyPath))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Не задан Jwt:SigningKeyPath. Временный ключ подписи допускается только в окружении Development.");
            }

            logger.LogWarning("Jwt:SigningKeyPath не задан, используется временный ключ подписи. Токены перестанут действовать после перезапуска.");
            Current = SigningKey.Generate();
        }
        else
        {
            Current = SigningKey.LoadFromPemFile(jwt.SigningKeyPath, requirePrivateKey: true);
        }

        var keys = new List<SigningKey> { Current };

        if (!string.IsNullOrWhiteSpace(jwt.PreviousSigningKeyPath))
        {
            var previous = SigningKey.LoadFromPemFile(jwt.PreviousSigningKeyPath, requirePrivateKey: false);
            if (previous.KeyId != Current.KeyId)
            {
                keys.Add(previous);
            }
        }

        ValidationKeys = keys;
        logger.LogInformation("Ключ подписи загружен, kid {KeyId}. Ключей для проверки: {KeyCount}.", Current.KeyId, keys.Count);
    }

    /// <summary>Ключ, которым подписываются новые токены.</summary>
    public SigningKey Current { get; }

    /// <summary>Текущий и предыдущий ключи: публикуются в JWKS и используются для проверки.</summary>
    public IReadOnlyList<SigningKey> ValidationKeys { get; }
}
