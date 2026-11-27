using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Регистрация и вход по email и паролю.
/// </summary>
public sealed class AuthService(
    IdentityServiceDbContext db,
    IPasswordHasher passwordHasher,
    IEmailNormalizer emailNormalizer,
    SessionService sessionService,
    IOptions<AccountLockoutOptions> lockoutOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    public async Task<Result<UserProfile>> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (!emailNormalizer.TryNormalize(email, out var cleanEmail, out var normalizedEmail))
        {
            return new Error(ErrorCode.Validation, "Некорректный email.");
        }

        if (!PasswordPolicy.IsSatisfiedBy(password))
        {
            return new Error(ErrorCode.Validation, $"Длина пароля должна быть от {PasswordPolicy.MinLength} до {PasswordPolicy.MaxLength} символов.");
        }

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return Error.EmailAlreadyRegistered;
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = cleanEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHasher.Hash(password),
            CreatedAt = now,
            UpdatedAt = now,
            Roles = [new UserRole { RoleId = RoleIds.User, AssignedAt = now }],
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Параллельная регистрация с тем же email успела раньше
            return Error.EmailAlreadyRegistered;
        }

        logger.LogInformation("Зарегистрирован пользователь {UserId}.", user.Id);
        return new UserProfile(user.Id, user.Email, [RoleCodes.User], user.CreatedAt, user.PasswordChangedAt);
    }

    /// <summary>
    /// Проверяет учётные данные и открывает новую сессию.
    /// Для несуществующего, заблокированного и временно заблокированного пользователя ответ
    /// и время ответа совпадают с ответом на неверный пароль.
    /// </summary>
    public async Task<Result<TokenPair>> LoginAsync(
        string email,
        string password,
        ClientInfo client,
        CancellationToken cancellationToken)
    {
        if (!emailNormalizer.TryNormalize(email, out _, out var normalizedEmail))
        {
            passwordHasher.SimulateVerification(password);
            return Error.InvalidCredentials;
        }

        var user = await db.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Role)
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            passwordHasher.SimulateVerification(password);
            return Error.InvalidCredentials;
        }

        var now = timeProvider.GetUtcNow();
        var check = passwordHasher.Verify(user.PasswordHash, password);

        if (user.LockoutEnd > now)
        {
            logger.LogInformation("Вход пользователя {UserId} отклонён: временная блокировка до {LockoutEnd}.", user.Id, user.LockoutEnd);
            return Error.InvalidCredentials;
        }

        if (check == PasswordCheckResult.Failed)
        {
            await RegisterFailedAttemptAsync(user.Id, now, cancellationToken);
            return Error.InvalidCredentials;
        }

        if (user.IsBlocked)
        {
            logger.LogInformation("Вход пользователя {UserId} отклонён: пользователь заблокирован.", user.Id);
            return Error.InvalidCredentials;
        }

        if (user.FailedLoginCount != 0 || user.LockoutEnd is not null)
        {
            await db.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(u => u.FailedLoginCount, 0)
                        .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null),
                    cancellationToken);
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
        {
            var newHash = passwordHasher.Hash(password);
            await db.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(u => u.PasswordHash, newHash)
                        .SetProperty(u => u.UpdatedAt, now),
                    cancellationToken);
        }

        return await sessionService.CreateSessionAsync(user, client, cancellationToken);
    }

    /// <summary>
    /// Атомарно увеличивает счётчик неудачных попыток. При достижении порога
    /// ставит временную блокировку и сбрасывает счётчик.
    /// </summary>
    private async Task RegisterFailedAttemptAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var maxAttempts = lockoutOptions.Value.MaxFailedAttempts;
        DateTimeOffset? lockoutEnd = now + lockoutOptions.Value.Duration;

        // В UPDATE правые части вычисляются по старым значениям строки, поэтому порядок не важен
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        u => u.LockoutEnd,
                        u => u.FailedLoginCount + 1 >= maxAttempts ? lockoutEnd : u.LockoutEnd)
                    .SetProperty(
                        u => u.FailedLoginCount,
                        u => u.FailedLoginCount + 1 >= maxAttempts ? 0 : u.FailedLoginCount + 1)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);
    }
}
