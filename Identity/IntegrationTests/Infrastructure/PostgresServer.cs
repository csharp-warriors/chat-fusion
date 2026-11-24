using Npgsql;
using Testcontainers.PostgreSql;

namespace ChatFusion.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Один контейнер PostgreSQL на весь запуск тестов. Каждая фабрика получает собственную базу данных.
/// </summary>
public static class PostgresServer
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    public static async Task<string> CreateDatabaseConnectionStringAsync()
    {
        var container = await Container.Value;
        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"identity_test_{Guid.NewGuid():N}",
        };

        return builder.ConnectionString;
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await container.StartAsync();
        return container;
    }
}
