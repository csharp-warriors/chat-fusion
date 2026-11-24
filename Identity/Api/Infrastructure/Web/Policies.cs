namespace ChatFusion.Identity.Api.Infrastructure.Web;

/// <summary>
/// Политики авторизации.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
}

/// <summary>
/// Политики ограничения частоты запросов.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Методы регистрации, входа и обновления токенов: лимит на IP-адрес.</summary>
    public const string Auth = "auth";
}
