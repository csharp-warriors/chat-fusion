using System.ComponentModel.DataAnnotations;

namespace ChatFusion.Identity.Api.Infrastructure.Configuration;

/// <summary>
/// Параметры выпуска JWT. Секция <c>Jwt</c>.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Публичный адрес сервиса, значение claim <c>iss</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "";

    /// <summary>Аудитория токенов, значение claim <c>aud</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "";

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>PEM-файл с закрытым ключом RSA. Обязателен вне окружения Development.</summary>
    public string? SigningKeyPath { get; set; }

    /// <summary>PEM-файл с предыдущим ключом: публикуется в JWKS на время ротации.</summary>
    public string? PreviousSigningKeyPath { get; set; }
}

/// <summary>
/// Сроки жизни сессий и refresh-токенов. Секция <c>Sessions</c>.
/// </summary>
public sealed class UserSessionOptions
{
    public const string SectionName = "Sessions";

    [Range(typeof(TimeSpan), "01:00:00", "365.00:00:00")]
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Сессия завершается, если токены не обновлялись дольше этого срока.</summary>
    [Range(typeof(TimeSpan), "00:10:00", "365.00:00:00")]
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromDays(7);

    /// <summary>В этот период повторный refresh уже использованным токеном считается гонкой, а не кражей.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan RotationGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Временная блокировка после неудачных попыток входа. Секция <c>Lockout</c>.
/// </summary>
public sealed class AccountLockoutOptions
{
    public const string SectionName = "Lockout";

    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Ограничение частоты запросов к методам входа. Секция <c>RateLimiting</c>.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Параметры cookie для браузерных клиентов. Секция <c>Cookies</c>.
/// </summary>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "Cookies";

    /// <summary>Передавать cookie только по HTTPS. Отключается только для локальной разработки.</summary>
    public bool Secure { get; set; } = true;
}

/// <summary>
/// Очистка устаревших сессий и токенов. Секция <c>Cleanup</c>.
/// </summary>
public sealed class CleanupOptions
{
    public const string SectionName = "Cleanup";

    public bool Enabled { get; set; } = true;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Сколько хранить завершённые сессии и истёкшие токены.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "365.00:00:00")]
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    [Range(1, 100_000)]
    public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// Доверенные прокси для заголовков <c>X-Forwarded-*</c>. Секция <c>ForwardedHeaders</c>.
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownProxies { get; set; } = [];

    /// <summary>Сети в нотации CIDR, например <c>172.16.0.0/12</c>.</summary>
    public string[] KnownNetworks { get; set; } = [];
}
