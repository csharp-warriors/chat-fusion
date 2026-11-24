using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Управление пользователями администратором.
/// </summary>
public sealed class AdminUserService(
    IdentityServiceDbContext db,
    SessionService sessionService,
    TimeProvider timeProvider,
    ILogger<AdminUserService> logger)
{
    public async Task<PagedList<AdminUserSummary>> ListAsync(
        int page,
        int pageSize,
        string? emailFilter,
        CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(emailFilter))
        {
            var normalizedFilter = emailFilter.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail.Contains(normalizedFilter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummary(
                u.Id,
                u.Email,
                u.Roles.Select(r => r.Role.Code).OrderBy(code => code).ToList(),
                u.IsBlocked,
                u.LockoutEnd,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedList<AdminUserSummary>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Заменяет набор ролей пользователя. Администратор не может снять роль <c>admin</c> сам с себя.
    /// </summary>
    public async Task<Result> SetRolesAsync(
        Guid actorId,
        Guid userId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken)
    {
        var codes = roleCodes
            .Select(code => code.Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .Distinct()
            .ToList();

        if (codes.Count == 0)
        {
            return new Error(ErrorCode.Validation, "Нужно указать хотя бы одну роль.");
        }

        var roles = await db.Roles.Where(r => codes.Contains(r.Code)).ToListAsync(cancellationToken);
        if (roles.Count != codes.Count)
        {
            var unknown = codes.Except(roles.Select(r => r.Code));
            return new Error(ErrorCode.UnknownRole, $"Неизвестные роли: {string.Join(", ", unknown)}.");
        }

        if (actorId == userId && !codes.Contains(RoleCodes.Admin))
        {
            return new Error(ErrorCode.SelfModificationForbidden, "Нельзя снять роль администратора с самого себя.");
        }

        var user = await db.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Error.UserNotFound;
        }

        var targetRoleIds = roles.Select(r => r.Id).ToHashSet();
        var now = timeProvider.GetUtcNow();

        user.Roles.RemoveAll(r => !targetRoleIds.Contains(r.RoleId));
        foreach (var roleId in targetRoleIds.Where(id => user.Roles.All(r => r.RoleId != id)))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = roleId, AssignedAt = now, AssignedBy = actorId });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Администратор {ActorId} назначил пользователю {UserId} роли: {Roles}.",
            actorId,
            userId,
            string.Join(", ", codes));
        return Result.Success();
    }

    /// <summary>
    /// Блокирует пользователя и завершает все его сессии.
    /// </summary>
    public async Task<Result> BlockAsync(Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        if (actorId == userId)
        {
            return new Error(ErrorCode.SelfModificationForbidden, "Нельзя заблокировать самого себя.");
        }

        var now = timeProvider.GetUtcNow();
        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.IsBlocked, true)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);

        if (updated == 0)
        {
            return Error.UserNotFound;
        }

        var revoked = await sessionService.RevokeAllUserSessionsAsync(userId, null, SessionRevokeReason.UserBlocked, cancellationToken);
        logger.LogInformation(
            "Администратор {ActorId} заблокировал пользователя {UserId}, завершено сессий: {RevokedSessions}.",
            actorId,
            userId,
            revoked);
        return Result.Success();
    }

    /// <summary>
    /// Снимает блокировку администратора и временную блокировку после неудачных попыток входа.
    /// </summary>
    public async Task<Result> UnblockAsync(Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.IsBlocked, false)
                    .SetProperty(u => u.FailedLoginCount, 0)
                    .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);

        if (updated == 0)
        {
            return Error.UserNotFound;
        }

        logger.LogInformation("Администратор {ActorId} разблокировал пользователя {UserId}.", actorId, userId);
        return Result.Success();
    }
}
