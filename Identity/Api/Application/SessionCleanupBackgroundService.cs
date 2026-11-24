using ChatFusion.Identity.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Периодически удаляет завершённые сессии и истёкшие refresh-токены.
/// </summary>
public sealed class SessionCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<SessionCleanupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Фоновая очистка сессий отключена.");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SessionService>().CleanupAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Ошибка фоновой очистки сессий.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
