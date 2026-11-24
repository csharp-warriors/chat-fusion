using System.Net;

namespace ChatFusion.Identity.Core.Domain;

/// <summary>
/// Сессия пользователя: один вход с одного устройства.
/// Идентификатор сессии попадает в claim <c>sid</c> access-токена.
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Время последнего обновления токенов.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Абсолютный срок жизни сессии.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public IPAddress? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public SessionRevokeReason? RevokeReason { get; set; }

    public User User { get; set; } = null!;

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// Причина завершения сессии.
/// </summary>
public enum SessionRevokeReason : short
{
    Logout = 1,
    Expired = 2,
    RefreshTokenReuse = 3,
    UserBlocked = 4,
    PasswordChanged = 5,
    RevokedByUser = 6,
}
