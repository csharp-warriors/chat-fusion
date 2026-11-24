using Microsoft.EntityFrameworkCore;

namespace ChatFusion.Identity.Core.Persistence;

/// <summary>
/// Общие настройки подключения для сервиса и мигратора.
/// </summary>
public static class DatabaseConfiguration
{
    public const string ConnectionStringName = "Identity";

    /// <summary>
    /// Сборка с миграциями. Миграции применяет только мигратор: сервис их не содержит и не запускает.
    /// </summary>
    public const string MigrationsAssembly = "ChatFusion.Identity.Migrator";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(MigrationsAssembly)
                .MigrationsHistoryTable(MigrationsHistoryTable, IdentityServiceDbContext.Schema))
            .UseSnakeCaseNamingConvention();
    }
}
