using System.Security.Claims;

namespace ChatFusion.Identity.Api.Infrastructure.Security;

/// <summary>
/// Пользователь и сессия текущего запроса, взятые из проверенного access-токена.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    Guid SessionId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId => ReadGuid(IdentityClaimTypes.Subject);

    public Guid SessionId => ReadGuid(IdentityClaimTypes.SessionId);

    private Guid ReadGuid(string claimType)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("Запрос не выполняется в контексте HTTP.");

        return Guid.TryParse(principal.FindFirstValue(claimType), out var value)
            ? value
            : throw new InvalidOperationException($"В токене нет корректного claim {claimType}.");
    }
}
