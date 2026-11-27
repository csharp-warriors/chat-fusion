using System.ComponentModel.DataAnnotations;
using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatFusion.Identity.Migrator;

/// <summary>
/// Регистрация сервисов мигратора. Используется программой мигратора и интеграционными тестами.
/// </summary>
public static class MigratorServiceCollectionExtensions
{
    /// <summary>
    /// Строка подключения для команды <c>script</c>, которой база данных не нужна.
    /// </summary>
    private const string OfflineConnectionString = "Host=localhost;Database=chatfusion_identity";

    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация: строка подключения, секции <c>Admin</c> и <c>PasswordHashing</c>.</param>
    /// <param name="requireConnectionString">
    /// <see langword="false"/> только для генерации SQL-скрипта без подключения к базе данных.
    /// </param>
    public static IServiceCollection AddIdentityMigrator(
        this IServiceCollection services,
        IConfiguration configuration,
        bool requireConnectionString = true)
    {
        services.AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection(PasswordHashingOptions.SectionName))
            .Validate(
                options => Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true),
                $"Некорректная секция {PasswordHashingOptions.SectionName}: число итераций вне допустимого диапазона.");
        services.AddOptions<AdminSeedOptions>().Bind(configuration.GetSection(AdminSeedOptions.SectionName));

        services.AddDbContext<IdentityServiceDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(DatabaseConfiguration.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (requireConnectionString)
                {
                    throw new InvalidOperationException(
                        $"Не задана строка подключения ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}. " +
                        $"Задайте её в appsettings.{{Environment}}.json, user-secrets или переменной ConnectionStrings__{DatabaseConfiguration.ConnectionStringName}.");
                }

                connectionString = OfflineConnectionString;
            }

            DatabaseConfiguration.Configure(options, connectionString);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IEmailNormalizer, EmailNormalizer>();
        services.AddScoped<DatabaseMigrator>();

        return services;
    }
}
