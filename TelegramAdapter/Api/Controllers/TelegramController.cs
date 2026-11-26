using ChatFusion.TelegramAdapter.Api.Models.Telegram;
using ChatFusion.TelegramAdapter.Api.Options;
using ChatFusion.TelegramAdapter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChatFusion.TelegramAdapter.Api.Controllers;

/// <summary>
/// Прием обновлений от Telegram Bot API
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class TelegramController(TelegramService service, IOptionsMonitor<AdapterOptions> adapterOptions, ILogger<TelegramController> logger) : ControllerBase
{
    #region Constants

    /// <summary>
    /// Наименование заголовка с секретом webhook-а
    /// </summary>
    public const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    #endregion

    #region Actions

    /// <summary>
    /// Webhook для получения обновлений из Telegram
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

    #endregion

}
