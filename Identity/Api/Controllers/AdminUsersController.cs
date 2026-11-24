using System.ComponentModel.DataAnnotations;
using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.Api.Infrastructure.Security;
using ChatFusion.Identity.Api.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Управление пользователями. Доступно только роли <c>admin</c>.
/// </summary>
[Route("api/v1/admin/users")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminUsersController(AdminUserService adminUserService, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>
    /// Возвращает страницу списка пользователей.
    /// </summary>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы, от 1 до 100.</param>
    /// <param name="email">Фрагмент email для поиска без учёта регистра.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Страница списка.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery, StringLength(256)] string? email = null,
        CancellationToken cancellationToken = default)
    {
        var users = await adminUserService.ListAsync(page, pageSize, email, cancellationToken);

        return Ok(new PagedResponse<AdminUserResponse>(
            users.Items
                .Select(u => new AdminUserResponse(u.Id, u.Email, u.Roles, u.IsBlocked, u.LockoutEnd, u.CreatedAt))
                .ToList(),
            users.Page,
            users.PageSize,
            users.TotalCount));
    }

    /// <summary>
    /// Заменяет набор ролей пользователя.
    /// </summary>
    /// <param name="id">Идентификатор пользователя.</param>
    /// <param name="request">Новый набор ролей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Роли назначены.</response>
    /// <response code="400">Неизвестная роль или пустой список.</response>
    /// <response code="404">Пользователь не найден.</response>
    /// <response code="409">Попытка снять роль администратора с самого себя.</response>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest request, CancellationToken cancellationToken)
    {
        var result = await adminUserService.SetRolesAsync(currentUser.UserId, id, request.Roles, cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }

    /// <summary>
    /// Блокирует пользователя и завершает все его сессии.
    /// </summary>
    /// <param name="id">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Пользователь заблокирован.</response>
    /// <response code="404">Пользователь не найден.</response>
    /// <response code="409">Попытка заблокировать самого себя.</response>
    [HttpPost("{id:guid}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Block(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminUserService.BlockAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }

    /// <summary>
    /// Снимает блокировку пользователя, в том числе временную после неудачных попыток входа.
    /// </summary>
    /// <param name="id">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Блокировка снята.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpPost("{id:guid}/unblock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminUserService.UnblockAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
