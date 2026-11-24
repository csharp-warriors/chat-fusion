using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Операции пользователя над своей учётной записью.
/// </summary>
public sealed class UserService(
    IdentityServiceDbContext db,
    IPasswordHasher passwordHasher,
    SessionService sessionService,
    TimeProvider timeProvider,
    ILogger<UserService> logger)
{
    public async Task<Result<UserProfile>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile(
                u.Id,
                u.Email,
                u.Roles.Select(r => r.Role.Code).OrderBy(code => code).ToList(),
                u.CreatedAt,
                u.PasswordChangedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null ? Error.UserNotFound : profile;
    }

    /// <summary>
    /// Меняет пароль и завершает все сессии пользователя, кроме текущей.
    /// </summary>
    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        Guid currentSessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsSatisfiedBy(newPassword))
        {
            return new Error(ErrorCode.Validation, $"Длина пароля должна быть от {PasswordPolicy.MinLength} до {PasswordPolicy.MaxLength} символов.");
        }

        var passwordHash = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.PasswordHash)
            .SingleOrDefaultAsync(cancellationToken);

        if (passwordHash is null)
        {
            return Error.UserNotFound;
        }

        if (passwordHasher.Verify(passwordHash, currentPassword) == PasswordCheckResult.Failed)
        {
            return Error.InvalidCurrentPassword;
        }

        var now = timeProvider.GetUtcNow();
        var newHash = passwordHasher.Hash(newPassword);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.PasswordHash, newHash)
                    .SetProperty(u => u.PasswordChangedAt, now)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);

        var revoked = await sessionService.RevokeAllUserSessionsAsync(
            userId,
            currentSessionId,
            SessionRevokeReason.PasswordChanged,
            cancellationToken);

        logger.LogInformation("Пользователь {UserId} сменил пароль, завершено других сессий: {RevokedSessions}.", userId, revoked);
        return Result.Success();
    }
}
