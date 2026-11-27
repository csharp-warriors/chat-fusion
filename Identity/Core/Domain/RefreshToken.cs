namespace ChatFusion.Identity.Core.Domain;

/// <summary>
/// Refresh-токен сессии. Сам токен не хранится, только его SHA-256.
/// Токены сессии образуют цепочку: при ротации старый помечается использованным и ссылается на новый.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Момент ротации. Повторное предъявление использованного токена означает его кражу.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public UserSession Session { get; set; } = null!;
}
