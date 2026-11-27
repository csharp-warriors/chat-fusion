using System.Collections.Concurrent;
using System.Net;
using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class MigrationAndCleanupTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Migrate_RepeatedRun_DoesNotDuplicateAdministrator()
    {
        await MigratorRunner.MigrateAsync(factory.ConnectionString);

        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        var admins = await db.Users
            .Where(u => u.NormalizedEmail == IdentityApiFactory.AdminEmail.ToUpperInvariant())
            .Select(u => u.Roles.Select(r => r.RoleId).ToList())
            .ToListAsync();

        admins.Should().ContainSingle().Which.Should().BeEquivalentTo([RoleIds.User, RoleIds.Admin]);
        (await db.Roles.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyFinishedSessionsOlderThanRetention()
    {
        var client = factory.CreateClient();
        var (_, finished) = await client.RegisterAndLoginAsync();
        await factory.CreateClient(finished.AccessToken).PostAsync("/api/v1/auth/logout", null);

        factory.Time.Advance(TimeSpan.FromDays(8));
        var (_, active) = await client.RegisterAndLoginAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SessionService>().CleanupAsync(CancellationToken.None);

        result.DeletedSessions.Should().BeGreaterThanOrEqualTo(1);
        (await client.RefreshAsync(active.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        (await db.UserSessions.AnyAsync(s => s.RevokedAt != null && s.RevokeReason == SessionRevokeReason.Logout)).Should().BeFalse();
    }
}

public sealed class RateLimitedApiFactory : IdentityApiFactory
{
    protected override void ConfigureSettings(IDictionary<string, string?> settings) =>
        settings["RateLimiting:PermitLimit"] = "3";
}

public sealed class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Login_ExceedingLimit_Returns429()
    {
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await client.LoginAsync(ApiClientExtensions.NewEmail())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await client.LoginAsync(ApiClientExtensions.NewEmail());
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        (await client.GetAsync("/.well-known/jwks.json")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public sealed class BrokenDatabaseApiFactory : IdentityApiFactory
{
    public override ValueTask InitializeAsync()
    {
        ConnectionString = "Host=127.0.0.1;Port=1;Database=missing;Username=nobody;Password=nothing;Timeout=2";
        return ValueTask.CompletedTask;
    }
}

public sealed class HealthWithoutDatabaseTests(BrokenDatabaseApiFactory factory) : IClassFixture<BrokenDatabaseApiFactory>
{
    [Fact]
    public async Task Ready_WithoutDatabase_Returns503_LiveStillReturns200()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}

public sealed class LogCapturingApiFactory : IdentityApiFactory
{
    public ConcurrentQueue<string> Messages { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(new CapturingLoggerProvider(Messages));
        });
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception) + exception);
    }
}

public sealed class LoggingTests(LogCapturingApiFactory factory) : IClassFixture<LogCapturingApiFactory>
{
    [Fact]
    public async Task Logs_DoNotContainPasswordsOrTokens()
    {
        var client = factory.CreateClient();
        var email = ApiClientExtensions.NewEmail();
        await client.RegisterAsync(email);
        await client.LoginAsync(email, "Wrong-Passw0rd");
        var tokens = await client.LoginSuccessfullyAsync(email);
        await client.RefreshAsync(tokens.RefreshToken);

        factory.Messages.Should().NotBeEmpty();
        factory.Messages.Should().NotContain(m => m.Contains(ApiClientExtensions.DefaultPassword, StringComparison.Ordinal));
        factory.Messages.Should().NotContain(m => m.Contains("Wrong-Passw0rd", StringComparison.Ordinal));
        factory.Messages.Should().NotContain(m => m.Contains(tokens.AccessToken, StringComparison.Ordinal));
        factory.Messages.Should().NotContain(m => m.Contains(tokens.RefreshToken!, StringComparison.Ordinal));
    }
}
