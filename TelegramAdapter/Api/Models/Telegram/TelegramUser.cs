using System.Text.Json.Serialization;

namespace ChatFusion.TelegramAdapter.Api.Models.Telegram;

/// <summary>
/// Пользователь Telegram
/// </summary>
public sealed class TelegramUser
{
    #region Properties

    /// <summary>
    /// Идентификатор
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Является ботом
    /// </summary>
    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    /// <summary>
    /// Имя
    /// </summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Фамилия
    /// </summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    /// <summary>
    /// Логин
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    #endregion
}
