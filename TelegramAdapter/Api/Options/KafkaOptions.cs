using System.ComponentModel.DataAnnotations;

namespace ChatFusion.TelegramAdapter.Api.Options;

/// <summary>
/// Настройки подключения к Kafka
/// </summary>
public sealed class KafkaOptions
{
    #region Constants

    /// <summary>
    /// Наименование секции
    /// </summary>
    public const string SectionName = "KafkaOptions";

    #endregion

    #region Properties

    /// <summary>
    /// Список брокеров
    /// </summary>
    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Топик для входящих сообщений
    /// </summary>
    [Required]
    public string IncomingTopic { get; set; } = "chat.incoming";

    #endregion
}
