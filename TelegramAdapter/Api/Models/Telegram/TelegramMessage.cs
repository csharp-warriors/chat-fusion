using System.Text.Json.Serialization;

namespace ChatFusion.TelegramAdapter.Api.Models.Telegram;

/// <summary>
/// Сообщение Telegram
/// </summary>
public sealed class TelegramMessage
{
    #region Properties

    /// <summary>
    /// Идентификатор сообщения
    /// </summary>
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    /// <summary>
    /// Отправитель
    /// </summary>
    [JsonPropertyName("from")]
    public TelegramUser? From { get; set; }

    /// <summary>
    /// Чат
    /// </summary>
    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; set; } = new();

    /// <summary>
    /// Дата отправки
    /// </summary>
    [JsonPropertyName("date")]
    public long Date { get; set; }

    /// <summary>
    /// Текст сообщения
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Заголовок
    /// </summary>
    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    #endregion
}
