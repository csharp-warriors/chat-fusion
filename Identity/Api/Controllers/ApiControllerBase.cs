using ChatFusion.Identity.Api.Application;
using Microsoft.AspNetCore.Mvc;

namespace ChatFusion.Identity.Api.Controllers;

/// <summary>
/// Базовый класс контроллеров: общие ответы об ошибках и преобразование ошибок сервисов в Problem Details.
/// </summary>
/// <remarks>
/// Атрибут <c>[Produces]</c> не используется: он заменил бы тип <c>application/problem+json</c> у ответов об ошибках.
/// </remarks>
[ApiController]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Ответ Problem Details с кодом ошибки в расширении <c>code</c>.
    /// </summary>
    protected ObjectResult ErrorResult(Error error)
    {
        var statusCode = error.Code switch
        {
            ErrorCode.Validation => StatusCodes.Status400BadRequest,
            ErrorCode.InvalidCurrentPassword => StatusCodes.Status400BadRequest,
            ErrorCode.UnknownRole => StatusCodes.Status400BadRequest,
            ErrorCode.InvalidCredentials => StatusCodes.Status401Unauthorized,
            ErrorCode.InvalidRefreshToken => StatusCodes.Status401Unauthorized,
            ErrorCode.CsrfTokenInvalid => StatusCodes.Status403Forbidden,
            ErrorCode.NotFound => StatusCodes.Status404NotFound,
            ErrorCode.EmailAlreadyRegistered => StatusCodes.Status409Conflict,
            ErrorCode.RefreshTokenAlreadyRotated => StatusCodes.Status409Conflict,
            ErrorCode.SelfModificationForbidden => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, error.Message);
        problem.Extensions["code"] = error.Code.ToString();

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}
