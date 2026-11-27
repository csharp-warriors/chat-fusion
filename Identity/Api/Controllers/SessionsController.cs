using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Сессии текущего пользователя на разных устройствах.
/// </summary>
[Route("api/v1/users/me/sessions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class SessionsController(SessionService sessionService, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>
    /// Возвращает активные сессии пользователя, последние сверху.
    /// </summary>
    /// <response code="200">Список сессий.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SessionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var sessions = await sessionService.GetActiveSessionsAsync(currentUser.UserId, currentUser.SessionId, cancellationToken);

        return Ok(sessions
            .Select(s => new SessionResponse(s.Id, s.CreatedAt, s.LastSeenAt, s.ExpiresAt, s.IpAddress, s.UserAgent, s.IsCurrent))
            .ToList());
    }

    /// <summary>
    /// Завершает сессию пользователя.
    /// </summary>
    /// <param name="id">Идентификатор сессии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Сессия завершена.</response>
    /// <response code="404">Активной сессии с таким идентификатором у пользователя нет.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var revoked = await sessionService.RevokeUserSessionAsync(
            currentUser.UserId,
            id,
            SessionRevokeReason.RevokedByUser,
            cancellationToken);

        return revoked ? NoContent() : ErrorResult(Error.SessionNotFound);
    }

    /// <summary>
    /// Завершает все сессии пользователя, кроме текущей.
    /// </summary>
    /// <response code="204">Сессии завершены.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOthers(CancellationToken cancellationToken)
    {
        await sessionService.RevokeAllUserSessionsAsync(
            currentUser.UserId,
            currentUser.SessionId,
            SessionRevokeReason.RevokedByUser,
            cancellationToken);

        return NoContent();
    }
}
