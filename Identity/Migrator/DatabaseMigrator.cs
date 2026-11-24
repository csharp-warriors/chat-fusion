using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.Migrator;

/// <summary>
/// Состояние миграций базы данных.
/// </summary>
/// <param name="DatabaseExists">База данных существует.</param>
/// <param name="Applied">Применённые миграции.</param>
/// <param name="Pending">Миграции, которые ещё не применены.</param>
public sealed record MigrationStatus(bool DatabaseExists, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);

/// <summary>
/// Создание базы данных и применение миграций сервиса Identity.
/// Повторный запуск <see cref="MigrateAsync"/> ничего не меняет.
/// </summary>
public sealed class DatabaseMigrator(
    IdentityServiceDbContext db,
    IPasswordHasher passwordHasher,
    IEmailNormalizer emailNormalizer,
    IOptions<AdminSeedOptions> adminOptions,
    TimeProvider timeProvider,
    ILogger<DatabaseMigrator> logger)
{
    /// <summary>
    /// Команда <c>migrate</c>: создаёт базу данных, если её нет, применяет миграции и создаёт первого администратора.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (!status.DatabaseExists)
        {
            logger.LogInformation("База данных {Database} не найдена и будет создана.", db.Database.GetDbConnection().Database);
        }

        if (status.Pending.Count > 0)
        {
            logger.LogInformation("Применяются миграции: {Migrations}.", string.Join(", ", status.Pending));
        }

        // Migrate сам создаёт базу данных, схему identity и таблицу истории миграций
        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation(
            status.Pending.Count > 0 ? "Миграции применены." : "База данных в актуальном состоянии, новых миграций нет.");

        await SeedAdministratorAsync(cancellationToken);
    }

    /// <summary>
    /// Команда <c>status</c>: применённые и ожидающие миграции. Базу данных не меняет.
    /// </summary>
    public async Task<MigrationStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var all = db.Database.GetMigrations().ToList();
        var creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync(cancellationToken))
        {
            return new MigrationStatus(DatabaseExists: false, Applied: [], Pending: all);
        }

        var applied = await creator.HasTablesAsync(cancellationToken)
            ? (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList()
            : [];

        return new MigrationStatus(DatabaseExists: true, applied, all.Except(applied).ToList());
    }

    /// <summary>
    /// Команда <c>script</c>: идемпотентный SQL-скрипт всех миграций для применения вручную (например, администратором БД).
    /// Подключение к базе данных не требуется.
    /// </summary>
    public string GenerateScript() =>
        db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        var options = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Email))
        {
            logger.LogInformation("Admin:Email не задан, первый администратор не создаётся.");
            return;
        }

        if (!emailNormalizer.TryNormalize(options.Email, out var email, out var normalizedEmail))
        {
            throw new InvalidOperationException("Admin:Email содержит некорректный email.");
        }

        var user = await db.Users
            .Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (user is null)
        {
            var password = await ReadPasswordAsync(options, cancellationToken);
            if (!PasswordPolicy.IsSatisfiedBy(password))
            {
                throw new InvalidOperationException(
                    $"Пароль администратора должен содержать от {PasswordPolicy.MinLength} до {PasswordPolicy.MaxLength} символов.");
            }

            user = new User
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = passwordHasher.Hash(password),
                CreatedAt = now,
                UpdatedAt = now,
                Roles =
                [
                    new UserRole { RoleId = RoleIds.User, AssignedAt = now },
                    new UserRole { RoleId = RoleIds.Admin, AssignedAt = now },
                ],
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Создан первый администратор {UserId}.", user.Id);
            return;
        }

        if (user.Roles.All(r => r.RoleId != RoleIds.Admin))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Admin, AssignedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Пользователю {UserId} назначена роль администратора.", user.Id);
            return;
        }

        logger.LogInformation("Администратор {UserId} уже существует.", user.Id);
    }

    private static async Task<string> ReadPasswordAsync(AdminSeedOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.PasswordFile))
        {
            if (!File.Exists(options.PasswordFile))
            {
                throw new InvalidOperationException($"Файл пароля администратора не найден: {options.PasswordFile}");
            }

            return (await File.ReadAllTextAsync(options.PasswordFile, cancellationToken)).Trim();
        }

        return options.Password
            ?? throw new InvalidOperationException("Для создания администратора задайте Admin:PasswordFile или Admin:Password.");
    }
}
