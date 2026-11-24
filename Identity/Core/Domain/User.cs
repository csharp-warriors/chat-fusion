namespace ChatFusion.Identity.Core.Domain;

/// <summary>
/// Учётная запись пользователя.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>Email в том виде, в котором его ввёл пользователь.</summary>
    public required string Email { get; set; }

    /// <summary>Email в верхнем регистре для поиска без учёта регистра.</summary>
    public required string NormalizedEmail { get; set; }

    /// <summary>Хеш пароля в формате с версией алгоритма.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>Пользователь заблокирован администратором.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>Число неудачных попыток входа подряд.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Окончание временной блокировки после неудачных попыток входа.</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset? PasswordChangedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Версия строки для контроля параллельных изменений (системный столбец xmin).</summary>
    public uint Version { get; set; }

    public List<UserRole> Roles { get; set; } = [];
}
