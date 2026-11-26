using System.ComponentModel.DataAnnotations;

namespace ChatFusion.TelegramAdapter.Api.Options;

/// <summary>
/// Настройки Telegram адаптера
/// </summary>
public sealed class AdapterOptions
{
    #region Constants

    /// <summary>
    /// Наименование секции
    /// </summary>
    public const string SectionName = "AdapterOptions";

    #endregion

    #region Properties

    /// <summary>
    /// Базовый URL Telegram Bot API
    /// </summary>
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Токен бота
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Секрет webhook-а
    /// </summary>
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    #endregion
}
