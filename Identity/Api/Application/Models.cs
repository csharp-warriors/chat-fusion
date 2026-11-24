using System.Net;

namespace ChatFusion.Identity.Api.Application;

/// <summary>Данные клиента, с которого выполняется вход.</summary>
public sealed record ClientInfo(IPAddress? IpAddress, string? UserAgent);

/// <summary>Пара токенов, выдаваемая при входе и обновлении.</summary>
public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record UserProfile(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PasswordChangedAt);

public sealed record SessionInfo(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsBlocked,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset CreatedAt);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CleanupResult(int DeletedRefreshTokens, int DeletedSessions);
