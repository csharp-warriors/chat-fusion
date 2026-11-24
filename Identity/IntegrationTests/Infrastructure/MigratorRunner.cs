using ChatFusion.Identity.Migrator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatFusion.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Запуск мигратора ChatFusion.Identity.Migrator с теми же сервисами, что и в его программе.
/// </summary>
public static class MigratorRunner
{
    public static Task MigrateAsync(string connectionString) =>
        RunAsync(connectionString, migrator => migrator.MigrateAsync(CancellationToken.None));

    public static async Task<MigrationStatus> GetStatusAsync(string connectionString)
    {
        MigrationStatus? status = null;
        await RunAsync(connectionString, async migrator => status = await migrator.GetStatusAsync(CancellationToken.None));
        return status!;
    }

    public static async Task<string> GenerateScriptAsync()
    {
        var script = "";
        await RunAsync(connectionString: null, migrator =>
        {
            script = migrator.GenerateScript();
            return Task.CompletedTask;
        });
        return script;
    }

    private static async Task RunAsync(string? connectionString, Func<DatabaseMigrator, Task> action)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = connectionString,
                ["PasswordHashing:Iterations"] = "10000",
                ["Admin:Email"] = IdentityApiFactory.AdminEmail,
                ["Admin:Password"] = IdentityApiFactory.AdminPassword,
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddIdentityMigrator(configuration, requireConnectionString: connectionString is not null);

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<DatabaseMigrator>());
    }
}
