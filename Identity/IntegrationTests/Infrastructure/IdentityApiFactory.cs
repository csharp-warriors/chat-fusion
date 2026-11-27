using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ChatFusion.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Сервис с собственной базой данных, управляемым временем и первым администратором.
/// </summary>
public class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Issuer = "https://identity.test";
    public const string Audience = "chatfusion-api";
    public const string AdminEmail = "admin@chatfusion.test";
    public const string AdminPassword = "Adm1n-Passw0rd";

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public string ConnectionString { get; protected set; } = "";

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await PostgresServer.CreateDatabaseConnectionStringAsync();

        // База данных создаётся и мигрирует отдельным проектом ChatFusion.Identity.Migrator, как в Docker
        await MigratorRunner.MigrateAsync(ConnectionString);
    }

    public HttpClient CreateClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected virtual void ConfigureSettings(IDictionary<string, string?> settings)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Identity"] = ConnectionString,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["PasswordHashing:Iterations"] = "10000",
            ["RateLimiting:PermitLimit"] = "100000",
            ["Cookies:Secure"] = "false",
            ["Cleanup:Enabled"] = "false",
            ["Scalar:Enabled"] = "true",
        };
        ConfigureSettings(settings);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}
