using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Npgsql;

namespace ChatFusion.Identity.IntegrationTests;

/// <summary>
/// Проект ChatFusion.Identity.Migrator: создание базы данных, состояние миграций и SQL-скрипт.
/// </summary>
public sealed class MigratorTests
{
    [Fact]
    public async Task Migrate_MissingDatabase_CreatesDatabaseAndAppliesAllMigrations()
    {
        var connectionString = await PostgresServer.CreateDatabaseConnectionStringAsync();

        var before = await MigratorRunner.GetStatusAsync(connectionString);
        before.DatabaseExists.Should().BeFalse();
        before.Applied.Should().BeEmpty();
        before.Pending.Should().NotBeEmpty();

        await MigratorRunner.MigrateAsync(connectionString);

        var after = await MigratorRunner.GetStatusAsync(connectionString);
        after.DatabaseExists.Should().BeTrue();
        after.Pending.Should().BeEmpty();
        after.Applied.Should().BeEquivalentTo(before.Pending);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name <> '__ef_migrations_history'",
            connection);
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(5);
    }

    [Fact]
    public async Task Script_WithoutDatabase_ReturnsIdempotentSql()
    {
        var script = await MigratorRunner.GenerateScriptAsync();

        script.Should().Contain("CREATE SCHEMA").And.Contain("CREATE TABLE identity.users");
        // Идемпотентный скрипт проверяет таблицу истории перед каждой миграцией
        script.Should().Contain("__ef_migrations_history").And.Contain("IF NOT EXISTS");
    }
}
