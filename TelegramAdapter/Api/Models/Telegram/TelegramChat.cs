using System.Text.Json.Serialization;

namespace ChatFusion.TelegramAdapter.Api.Models.Telegram;

/// <summary>
/// Чат Telegram
/// </summary>
public sealed class TelegramChat
{
    #region Properties

    /// <summary>
    /// Идентификатор
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Тип чата
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    #endregion
}
