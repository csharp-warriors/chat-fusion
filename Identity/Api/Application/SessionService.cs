using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Сессии пользователей и ротация refresh-токенов.
/// </summary>
public sealed class SessionService(
    IdentityServiceDbContext db,
    ITokenService tokenService,
    IOptions<UserSessionOptions> sessionOptions,
    IOptions<CleanupOptions> cleanupOptions,
    TimeProvider timeProvider,
    ILogger<SessionService> logger)
{
    private const int MaxUserAgentLength = 512;

    /// <summary>
    /// Открывает сессию и выдаёт первую пару токенов. У пользователя должны быть загружены роли.
    /// </summary>
    public async Task<TokenPair> CreateSessionAsync(User user, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + sessionOptions.Value.AbsoluteLifetime,
            IpAddress = client.IpAddress,
            UserAgent = client.UserAgent is { Length: > MaxUserAgentLength } userAgent
                ? userAgent[..MaxUserAgentLength]
                : client.UserAgent,
        };

        var (rawToken, refreshToken) = CreateRefreshToken(session, Guid.CreateVersion7(), now);

        db.UserSessions.Add(session);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Открыта сессия {SessionId} пользователя {UserId}.", session.Id, user.Id);
        return IssueTokens(user, session, rawToken, refreshToken);
    }

    /// <summary>
    /// Обменивает refresh-токен на новую пару токенов.
    /// Повторное предъявление использованного токена после периода допуска завершает сессию.
    /// </summary>
    public async Task<Result<TokenPair>> RefreshAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > RefreshTokenGenerator.MaxTokenLength)
        {
            return Error.InvalidRefreshToken;
        }

        var tokenHash = RefreshTokenGenerator.Hash(rawToken);
        var token = await db.RefreshTokens
            .Include(t => t.Session)
            .ThenInclude(s => s.User)
            .ThenInclude(u => u.Roles)
            .ThenInclude(r => r.Role)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            return Error.InvalidRefreshToken;
        }

        var session = token.Session;
        var now = timeProvider.GetUtcNow();
        var options = sessionOptions.Value;

        if (session.RevokedAt is not null)
        {
            return Error.InvalidRefreshToken;
        }

        if (token.UsedAt is { } usedAt)
        {
            if (now - usedAt <= options.RotationGracePeriod)
            {
                return Error.RefreshTokenAlreadyRotated;
            }

            await RevokeSessionAsync(session.Id, SessionRevokeReason.RefreshTokenReuse, now, cancellationToken);
            logger.LogWarning(
                "Повторно предъявлен использованный refresh-токен. Сессия {SessionId} пользователя {UserId} завершена.",
                session.Id,
                session.UserId);
            return Error.InvalidRefreshToken;
        }

        if (session.ExpiresAt <= now || token.ExpiresAt <= now)
        {
            await RevokeSessionAsync(session.Id, SessionRevokeReason.Expired, now, cancellationToken);
            return Error.InvalidRefreshToken;
        }

        if (session.User.IsBlocked)
        {
            await RevokeSessionAsync(session.Id, SessionRevokeReason.UserBlocked, now, cancellationToken);
            return Error.InvalidRefreshToken;
        }

        var newTokenId = Guid.CreateVersion7();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Атомарная ротация: из параллельных запросов с одним токеном строку обновит только первый
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.UsedAt, now)
                    .SetProperty(t => t.ReplacedById, newTokenId),
                cancellationToken);

        if (rotated == 0)
        {
            return Error.RefreshTokenAlreadyRotated;
        }

        var (newRawToken, newToken) = CreateRefreshToken(session, newTokenId, now);
        db.RefreshTokens.Add(newToken);
        session.LastSeenAt = now;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return IssueTokens(session.User, session, newRawToken, newToken);
    }

    public async Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lastSeenThreshold = now - sessionOptions.Value.InactivityTimeout;

        var sessions = await db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId
                && s.RevokedAt == null
                && s.ExpiresAt > now
                && s.LastSeenAt > lastSeenThreshold)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(s => new SessionInfo(
                s.Id,
                s.CreatedAt,
                s.LastSeenAt,
                s.ExpiresAt,
                s.IpAddress?.ToString(),
                s.UserAgent,
                s.Id == currentSessionId))
            .ToList();
    }

    /// <summary>
    /// Завершает сессию пользователя. Возвращает <c>false</c>, если активной сессии с таким идентификатором у него нет.
    /// </summary>
    public async Task<bool> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        SessionRevokeReason reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var revoked = await db.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.RevokedAt, now)
                    .SetProperty(s => s.RevokeReason, reason),
                cancellationToken);

        return revoked > 0;
    }

    /// <summary>
    /// Завершает все сессии пользователя, кроме указанной.
    /// </summary>
    public async Task<int> RevokeAllUserSessionsAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionRevokeReason reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await db.UserSessions
            .Where(s => s.UserId == userId
                && s.RevokedAt == null
                && (exceptSessionId == null || s.Id != exceptSessionId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.RevokedAt, now)
                    .SetProperty(s => s.RevokeReason, reason),
                cancellationToken);
    }

    /// <summary>
    /// Удаляет завершённые сессии и истёкшие токены старше срока хранения. Удаление идёт пакетами.
    /// </summary>
    public async Task<CleanupResult> CleanupAsync(CancellationToken cancellationToken)
    {
        var cleanup = cleanupOptions.Value;
        var threshold = timeProvider.GetUtcNow() - cleanup.RetentionPeriod;
        var inactivityThreshold = threshold - sessionOptions.Value.InactivityTimeout;

        var deletedSessions = await DeleteInBatchesAsync(
            db.UserSessions.Where(s => s.RevokedAt < threshold
                || s.ExpiresAt < threshold
                || s.LastSeenAt < inactivityThreshold),
            s => s.Id,
            ids => db.UserSessions.Where(s => ids.Contains(s.Id)),
            cleanup.BatchSize,
            cancellationToken);

        var deletedTokens = await DeleteInBatchesAsync(
            db.RefreshTokens.Where(t => t.ExpiresAt < threshold),
            t => t.Id,
            ids => db.RefreshTokens.Where(t => ids.Contains(t.Id)),
            cleanup.BatchSize,
            cancellationToken);

        if (deletedSessions > 0 || deletedTokens > 0)
        {
            logger.LogInformation(
                "Очистка: удалено сессий {DeletedSessions}, refresh-токенов {DeletedTokens}.",
                deletedSessions,
                deletedTokens);
        }

        return new CleanupResult(deletedTokens, deletedSessions);
    }

    private static async Task<int> DeleteInBatchesAsync<TEntity>(
        IQueryable<TEntity> candidates,
        System.Linq.Expressions.Expression<Func<TEntity, Guid>> idSelector,
        Func<List<Guid>, IQueryable<TEntity>> byIds,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            var ids = await candidates.Select(idSelector).OrderBy(id => id).Take(batchSize).ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return total;
            }

            var deleted = await byIds(ids).ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0)
            {
                // Записи удалены параллельно другим экземпляром сервиса
                return total;
            }

            total += deleted;
        }
    }

    private Task RevokeSessionAsync(Guid sessionId, SessionRevokeReason reason, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.UserSessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.RevokedAt, now)
                    .SetProperty(s => s.RevokeReason, reason),
                cancellationToken);

    private (string RawToken, RefreshToken Token) CreateRefreshToken(UserSession session, Guid tokenId, DateTimeOffset now)
    {
        var inactivityExpiry = now + sessionOptions.Value.InactivityTimeout;
        var rawToken = RefreshTokenGenerator.Generate();

        var token = new RefreshToken
        {
            Id = tokenId,
            SessionId = session.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = inactivityExpiry < session.ExpiresAt ? inactivityExpiry : session.ExpiresAt,
        };

        return (rawToken, token);
    }

    private TokenPair IssueTokens(User user, UserSession session, string rawRefreshToken, RefreshToken refreshToken)
    {
        var roles = user.Roles.Select(r => r.Role.Code).Order(StringComparer.Ordinal).ToList();
        var accessToken = tokenService.CreateAccessToken(user.Id, session.Id, roles);

        return new TokenPair(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshToken.ExpiresAt);
    }
}
