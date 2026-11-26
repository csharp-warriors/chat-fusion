using System.Text.Json.Serialization;

namespace ChatFusion.TelegramAdapter.Api.Models.Telegram;

/// <summary>
/// Обновление Telegram
/// </summary>
public sealed class TelegramUpdate
{
    #region Properties

    /// <summary>
    /// Уникальный идентификатор обновления
    /// </summary>
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    /// <summary>
    /// Новое входящее сообщение
    /// </summary>
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }

    #endregion
}
