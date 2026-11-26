using ChatFusion.TelegramAdapter.Api.Models.Telegram;
using ChatFusion.TelegramAdapter.Api.Options;
using ChatFusion.TelegramAdapter.Api.Services;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace ChatFusion.TelegramAdapter.Api.Controllers;

/// <summary>
/// Прием обновлений от Telegram Bot API
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class TelegramController(TelegramService service, IOptionsMonitor<AdapterOptions> adapterOptions, IOptions<KafkaOptions> kafkaOptions, ILogger<TelegramController> logger) : ControllerBase
{
    #region Constants

    /// <summary>
    /// Наименование заголовка с секретом webhook-а
    /// </summary>
    public const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    #endregion

    #region Actions

    /// <summary>
    /// Webhook для получения сообщений из Telegram
    /// </summary>
    /// <param name="update">Обновление Telegram</param>
    /// <param name="secretToken">Секрет</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат</returns>
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate update, [FromHeader(Name = SecretTokenHeader)] string? secretToken, CancellationToken cancellationToken)
    {
        logger.LogInformation("Получено обновление Telegram {UpdateId}", update.UpdateId);

        if (adapterOptions.CurrentValue.WebhookSecret != secretToken)
        {
            logger.LogWarning("Обновление {UpdateId} отклонено: неверный секрет webhook-а", update.UpdateId);
            return Unauthorized();
        }

        var result = await service.HandleAsync(update, cancellationToken);
        logger.LogInformation("Обновление {UpdateId} обработано с результатом {Result}", update.UpdateId, result);
        return Ok();
    }

    /// <summary>
    /// Выводит последнее сообщение из Kafka
    /// </summary>
    [HttpGet("kafka/latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetLatestKafkaMessage()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = $"telegram-debug-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(kafkaOptions.Value.IncomingTopic);

        try
        {
            var latest = consumer.Consume(TimeSpan.FromMilliseconds(1000));
            while (latest is not null)
            {
                var next = consumer.Consume(TimeSpan.Zero);
                if (next is null)
                {
                    break;
                }

                latest = next;
            }

            if (latest is null)
            {
                return NoContent();
            }

            return Ok(new
            {
                latest.Message.Key,
                latest.Message.Value,
                Headers = latest.Message.Headers.ToDictionary(
                    header => header.Key,
                    header => Encoding.UTF8.GetString(header.GetValueBytes())),
                latest.Topic,
                Partition = latest.Partition.Value,
                Offset = latest.Offset.Value,
            });
        }
        finally
        {
            consumer.Close();
        }
    }

    #endregion
}
