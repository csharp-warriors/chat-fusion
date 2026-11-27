using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Учётная запись текущего пользователя.
/// </summary>
[Route("api/v1/users/me")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class UsersController(UserService userService, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>
    /// Возвращает профиль текущего пользователя.
    /// </summary>
    /// <response code="200">Профиль.</response>
    /// <response code="404">Учётная запись удалена.</response>
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await userService.GetProfileAsync(currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!);
        }

        var profile = result.Value;
        return Ok(new UserResponse(profile.Id, profile.Email, profile.Roles, profile.CreatedAt, profile.PasswordChangedAt));
    }

    /// <summary>
    /// Меняет пароль и завершает все остальные сессии пользователя.
    /// </summary>
    /// <response code="204">Пароль изменён.</response>
    /// <response code="400">Неверный текущий пароль или новый пароль не соответствует требованиям.</response>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.ChangePasswordAsync(
            currentUser.UserId,
            currentUser.SessionId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
