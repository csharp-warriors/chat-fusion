using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Api.Infrastructure.Security;
using ChatFusion.Identity.Api.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Регистрация, вход, обновление токенов и выход.
/// </summary>
[Route("api/v1/auth")]
public sealed class AuthController(
    AuthService authService,
    SessionService sessionService,
    RefreshTokenCookies refreshTokenCookies,
    ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>
    /// Регистрирует пользователя с ролью <c>user</c>.
    /// </summary>
    /// <response code="201">Пользователь создан.</response>
    /// <response code="400">Некорректный email или пароль.</response>
    /// <response code="409">Email уже зарегистрирован.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!);
        }

        var user = result.Value;
        return StatusCode(
            StatusCodes.Status201Created,
            new UserResponse(user.Id, user.Email, user.Roles, user.CreatedAt, user.PasswordChangedAt));
    }

    /// <summary>
    /// Выполняет вход и открывает новую сессию.
    /// </summary>
    /// <remarks>
    /// Браузерный клиент передаёт заголовок <c>X-Refresh-Token-Transport: cookie</c>:
    /// тогда refresh-токен устанавливается в cookie <c>HttpOnly</c> и не возвращается в теле ответа.
    /// </remarks>
    /// <response code="200">Вход выполнен.</response>
    /// <response code="401">Неверный email или пароль.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var client = new ClientInfo(HttpContext.Connection.RemoteIpAddress, Request.Headers.UserAgent.ToString());
        var result = await authService.LoginAsync(request.Email, request.Password, client, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!);
        }

        return Ok(CreateTokenResponse(result.Value, RefreshTokenCookies.IsCookieTransportRequested(Request)));
    }

    /// <summary>
    /// Обменивает refresh-токен на новую пару токенов. Старый refresh-токен становится недействительным.
    /// </summary>
    /// <remarks>
    /// Если тот же токен предъявлен повторно в течение короткого периода (параллельный запрос из другой вкладки),
    /// возвращается 409: клиент должен повторить запрос с актуальным токеном. Повтор после этого периода
    /// считается кражей токена, и сессия завершается.
    /// </remarks>
    /// <response code="200">Токены обновлены.</response>
    /// <response code="401">Токен недействителен, нужен повторный вход.</response>
    /// <response code="403">В режиме cookie отсутствует или неверен CSRF-токен.</response>
    /// <response code="409">Токен уже обновлён параллельным запросом.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken;
        var cookieTransport = false;

        if (string.IsNullOrEmpty(rawToken))
        {
            rawToken = RefreshTokenCookies.ReadRefreshToken(Request);
            cookieTransport = rawToken is not null;

            if (cookieTransport && !RefreshTokenCookies.IsCsrfTokenValid(Request))
            {
                return ErrorResult(Error.CsrfTokenInvalid);
            }
        }

        var result = await sessionService.RefreshAsync(rawToken, cancellationToken);
        if (!result.IsSuccess)
        {
            if (cookieTransport && result.Error!.Code == ErrorCode.InvalidRefreshToken)
            {
                refreshTokenCookies.Clear(Response);
            }

            return ErrorResult(result.Error!);
        }

        return Ok(CreateTokenResponse(result.Value, cookieTransport));
    }

    /// <summary>
    /// Завершает текущую сессию. Access-токен остаётся действительным до окончания срока (не более 15 минут).
    /// </summary>
    /// <response code="204">Сессия завершена.</response>
    /// <response code="401">Нет действительного access-токена.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessionService.RevokeUserSessionAsync(
            currentUser.UserId,
            currentUser.SessionId,
            SessionRevokeReason.Logout,
            cancellationToken);

        refreshTokenCookies.Clear(Response);
        return NoContent();
    }

    private TokenResponse CreateTokenResponse(TokenPair tokens, bool cookieTransport)
    {
        if (cookieTransport)
        {
            refreshTokenCookies.Write(Response, tokens);
        }

        return new TokenResponse
        {
            AccessToken = tokens.AccessToken,
            AccessTokenExpiresAt = tokens.AccessTokenExpiresAt,
            RefreshToken = cookieTransport ? null : tokens.RefreshToken,
            RefreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
        };
    }
}
