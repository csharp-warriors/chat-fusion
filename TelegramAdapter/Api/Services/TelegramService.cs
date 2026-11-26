using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ChatFusion.TelegramAdapter.Api.Models.Telegram;
using ChatFusion.TelegramAdapter.Api.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatFusion.TelegramAdapter.Api.Services;

/// <summary>
/// Результат обработки обновления Telegram
/// </summary>
public enum WebhookHandleResult
{
    /// <summary>
    /// Сообщение опубликовано
    /// </summary>
    Published,
    /// <summary>
    /// В обновлении нет нового сообщения
    /// </summary>
    NoMessage,
    /// <summary>
    /// Сообщение отправлено ботом
    /// </summary>
    SkippedBot,
    /// <summary>
    /// Сообщение уже обрабатывалось
    /// </summary>
    Duplicate
}

/// <summary>
/// Сервис обработки обновлений Telegram
/// </summary>
public sealed class TelegramService(IConnectionMultiplexer redis, IProducer<string, string> producer, IOptions<KafkaOptions> kafkaOptions, ILogger<TelegramService> logger)
{
    #region Constants

    /// <summary>
    /// Опции Json сериализации
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), };
    /// <summary>
    /// Таймаут дедупликации
    /// </summary>
    private static readonly TimeSpan DeduplicationTtl = TimeSpan.FromHours(24);
    /// <summary>
    /// Наименование канала
    /// </summary>
    public const string Channel = "telegram";

    #endregion

    #region Methods

    /// <summary>
    /// Обновление
    /// </summary>
    /// <param name="update">Обновление Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат</returns>
    public async Task<WebhookHandleResult> HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            logger.LogInformation("Обновление {UpdateId} не содержит message", update.UpdateId);
            return WebhookHandleResult.NoMessage;
        }

        var chatId = message.Chat.Id.ToString();
        var messageId = message.MessageId.ToString();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["UpdateId"] = update.UpdateId,
            ["ExternalChatId"] = chatId,
            ["ExternalMessageId"] = messageId,
        });

        if (message.From?.IsBot == true)
        {
            logger.LogInformation("Сообщение {ExternalChatId:l}:{ExternalMessageId:l} от бота {BotId}",chatId, messageId, message.From.Id);
            return WebhookHandleResult.SkippedBot;
        }

        var deduplicationKey = $"dedup:telegram:{chatId}:{messageId}";
        if (await redis.GetDatabase().KeyExistsAsync(deduplicationKey))
        {
            logger.LogInformation("Сообщение {ExternalChatId:l}:{ExternalMessageId:l} уже обработано", chatId, messageId);
            return WebhookHandleResult.Duplicate;
        }

        var kafkaMessage = new Message<string, string>
        {
            Key = $"{Channel}:{chatId}",
            Value = JsonSerializer.Serialize(update, JsonOptions),
            Headers = new Headers {{ "source", Encoding.UTF8.GetBytes(Channel) }}
        };

        var result = await producer.ProduceAsync(kafkaOptions.Value.IncomingTopic, kafkaMessage, cancellationToken);
        logger.LogInformation("Сообщение {ExternalChatId:l}:{ExternalMessageId:l} опубликовано в {Topic:l} (partition {Partition}, offset {Offset})", chatId, messageId, result.Topic, result.Partition.Value, result.Offset.Value);
        await redis.GetDatabase().StringSetAsync(deduplicationKey, DateTimeOffset.UtcNow.ToString("O"), DeduplicationTtl);
        logger.LogInformation("Сообщение {ExternalChatId:l}:{ExternalMessageId:l} обработано", chatId, messageId);
        return WebhookHandleResult.Published;
    }

    #endregion
}
