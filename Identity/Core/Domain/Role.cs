namespace ChatFusion.Identity.Core.Domain;

/// <summary>
/// Глобальная роль пользователя.
/// </summary>
public sealed class Role
{
    public short Id { get; set; }

    /// <summary>Код роли, попадает в claim <c>role</c> токена.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }
}

/// <summary>
/// Коды встроенных ролей.
/// </summary>
public static class RoleCodes
{
    public const string User = "user";
    public const string Admin = "admin";
}

/// <summary>
/// Идентификаторы встроенных ролей, создаваемых миграцией.
/// </summary>
public static class RoleIds
{
    public const short User = 1;
    public const short Admin = 2;
}
